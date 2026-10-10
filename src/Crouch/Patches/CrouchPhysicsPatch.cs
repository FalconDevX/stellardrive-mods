using System;
using HarmonyLib;
using MelonLoader;
using Players.Physics;
using Players.Physics.Model;
using Players.Values;
using UnityEngine;

namespace Crouch.Patches
{
    // Kucanie w fizyce chodzenia. Gra ma już akcję "crouch" (VerticalInput < 0), ale na ziemi jej nie używa -
    // w kosmosie (brak gruntu) dalej działa jako opadanie.
    //
    // Na czas jednego kroku fizyki zmniejszamy kapsułę gracza (prefab współdzielony przez wszystkich graczy,
    // więc przywracamy go w Finalizerze) i spowalniamy chód. Stan kucania jest liczony od nowa w każdym kroku
    // z wejścia i pozycji, więc działa też przy powtarzaniu kroków przez predykcję sieciową.
    // Kamera opuszcza się sama, bo jest liczona od środka kapsuły.
    [HarmonyPatch(typeof(PlayerWalkingPhysics), nameof(PlayerWalkingPhysics.ProcessPhysicsStep))]
    internal static class CrouchPhysicsPatch
    {
        private static readonly AccessTools.FieldRef<PlayerWalkingPhysics, CapsuleCollider> ColliderRef =
            AccessTools.FieldRefAccess<PlayerWalkingPhysics, CapsuleCollider>("_playerColliderPrefab");

        private static readonly AccessTools.FieldRef<PlayerWalkingPhysics, PlayerProperties> PropertiesRef =
            AccessTools.FieldRefAccess<PlayerWalkingPhysics, PlayerProperties>("_properties");

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[32];
        private static int _layerMask = -1;
        private static bool _loggedError;

        internal struct Saved
        {
            public bool Active;
            public CapsuleCollider Collider;
            public PlayerMovementSettings Settings;
            public float Height;
            public float WalkingSpeed;
            public float SprintingSpeed;
            public float SprintingAcceleration;
            public float WalkingAcceleration;
            public float GroundCheckDistance;
        }

        private static void Prefix(PlayerWalkingPhysics __instance, PlayerPhysicsStepContext context, out Saved __state)
        {
            __state = default;
            try
            {
                Apply(__instance, context, ref __state);
            }
            catch (Exception e)
            {
                Restore(__state);
                __state = default;
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<CrouchMod>.Logger.Error("Błąd kucania (dalsze pomijam): " + e);
                }
            }
        }

        private static void Finalizer(Saved __state)
        {
            Restore(__state);
        }

        private static void Apply(PlayerWalkingPhysics physics, PlayerPhysicsStepContext context, ref Saved saved)
        {
            var state = context.State;
            // Bez gruntu (skok, kosmos, brak grawitacji) klawisz zostaje opadaniem jak w oryginale.
            if (!state.IsOnGround || context.DeltaTime <= 0f)
            {
                return;
            }

            CapsuleCollider collider = ColliderRef(physics);
            PlayerProperties properties = PropertiesRef(physics);
            if (collider == null || properties == null || properties.movementSettings == null)
            {
                return;
            }

            if (_layerMask == -1)
            {
                _layerMask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Ship"));
            }

            float fullHeight = collider.height;
            float factor = Mathf.Clamp(CrouchMod.HeightFactor.Value, 0.3f, 0.95f);
            float crouchHeight = Mathf.Max(fullHeight * factor, collider.radius * 2f);
            float delta = fullHeight - crouchHeight;
            if (delta <= 0.01f)
            {
                return;
            }

            PhysicsScene scene = context.Scene.GetPhysicsScene();
            Vector3 up = (state.Rotation * Vector3.up).normalized;
            bool wantsCrouch = context.Input.VerticalInput < 0f;
            bool crouched = wantsCrouch
                || (IsLow(scene, state.Position, up, fullHeight, delta) && !CanStand(scene, state.Position + up * (delta * 0.5f), up, fullHeight, collider.radius));
            if (!crouched)
            {
                return;
            }

            PlayerMovementSettings settings = properties.movementSettings;
            saved = new Saved
            {
                Active = true,
                Collider = collider,
                Settings = settings,
                Height = collider.height,
                WalkingSpeed = settings.walkingSpeed,
                SprintingSpeed = settings.sprintingSpeed,
                WalkingAcceleration = settings.walkingAcceleration,
                SprintingAcceleration = settings.sprintingAcceleration,
                GroundCheckDistance = settings.collisionSettings != null ? settings.collisionSettings.groundSphereCheckDistance : 0f,
            };

            collider.height = crouchHeight;
            float speed = settings.walkingSpeed * Mathf.Clamp(CrouchMod.SpeedFactor.Value, 0.1f, 1f);
            settings.walkingSpeed = speed;
            settings.sprintingSpeed = speed;
            settings.sprintingAcceleration = settings.walkingAcceleration;
            // Dłuższy test gruntu: po skurczeniu kapsuły gra "dociąga" postać do ziemi zamiast spadać.
            if (settings.collisionSettings != null)
            {
                settings.collisionSettings.groundSphereCheckDistance += delta;
            }
        }

        private static void Restore(Saved saved)
        {
            if (!saved.Active)
            {
                return;
            }
            if (saved.Collider != null)
            {
                saved.Collider.height = saved.Height;
            }
            if (saved.Settings != null)
            {
                saved.Settings.walkingSpeed = saved.WalkingSpeed;
                saved.Settings.sprintingSpeed = saved.SprintingSpeed;
                saved.Settings.walkingAcceleration = saved.WalkingAcceleration;
                saved.Settings.sprintingAcceleration = saved.SprintingAcceleration;
                if (saved.Settings.collisionSettings != null)
                {
                    saved.Settings.collisionSettings.groundSphereCheckDistance = saved.GroundCheckDistance;
                }
            }
        }

        // Czy środek postaci jest nisko nad ziemią, tzn. postać jest jeszcze w pozycji kucającej.
        private static bool IsLow(PhysicsScene scene, Vector3 center, Vector3 up, float fullHeight, float delta)
        {
            int count = scene.Raycast(center, -up, Hits, fullHeight * 0.5f + 0.1f, _layerMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider != null && !Hits[i].collider.CompareTag("Player") && Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                }
            }
            return nearest < fullHeight * 0.5f - delta * 0.25f;
        }

        // Czy nad głową jest miejsce na wstanie (sprawdzamy górną połowę pełnej kapsuły).
        private static bool CanStand(PhysicsScene scene, Vector3 standCenter, Vector3 up, float fullHeight, float radius)
        {
            float r = radius * 0.95f;
            Vector3 top = standCenter + up * (fullHeight * 0.5f - radius);
            int count = scene.OverlapCapsule(standCenter, top, r, Overlaps, _layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Overlaps[i] != null && !Overlaps[i].CompareTag("Player"))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
