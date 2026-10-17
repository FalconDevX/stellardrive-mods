using System;
using Core.Values;
using HarmonyLib;
using MelonLoader;
using Players.LocalClient.Controls.Model;
using Players.Network;
using Players.Network.Model;
using Players.Physics;
using UnityEngine;
using Water.Buoyancy;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;

namespace Water.Swimming
{
    [HarmonyPatch(typeof(LocalPlayerPhysicsPredictions), "ProcessPhysicsStep")]
    internal static class SwimPredictionContextPatch
    {
        internal static bool Local;
        private static void Prefix() => Local = true;
        private static void Finalizer() => Local = false;
    }

    [HarmonyPatch(typeof(PlayerWalkingPhysics), "ProcessControlInputs")]
    internal static class SwimmingPatch
    {
        private const float FloatDepth = 1.25f, SwimDepth = 0.9f;
        private const float SwimSpeed = 3.2f, SprintSwimSpeed = 5.5f, SurfaceSpeed = 3f, Acceleration = 7f;

        private static readonly LakeCache Lake = new LakeCache();
        private static bool _failed;
        private static bool _localWet;
        private static float _lastSplash;

        internal static float LocalDepth { get; private set; } = -100f;
        internal static float LocalSpeed { get; private set; }
        internal static bool LocalSwimming { get; private set; }

        private static void Postfix(ref PlayerPhysicsState __0, PlayerControlInput __1, Vector3 ____currentUp, Vector3 ____currentGravity, float ____deltaTime, ISpace ____currentSpace)
        {
            if (_failed || !WaterMod.Enabled.Value || !WaterMod.Swimming.Value)
            {
                return;
            }
            try
            {
                Step(ref __0, __1, ____currentUp, ____currentGravity, ____deltaTime, ____currentSpace);
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Error($"Swimming disabled: {e}");
            }
        }

        private static void Step(ref PlayerPhysicsState state, PlayerControlInput input, Vector3 up, Vector3 gravity, float dt, ISpace space)
        {
            if (!(space is PlanetSurfaceSpace planet) || planet.PlanetId != WaterLevel.PlanetId || dt <= 0f || gravity == Vector3.zero || !WaterLevel.Ready())
            {
                SetLocal(-100f, default, Vector3.zero);
                return;
            }
            Vector3Double position = planet.Center + (Vector3Double)state.Position;
            float depth = WaterLevel.SeaRadius - (float)position.Magnitude;
            if (depth <= 0f || !WaterLevel.IsLake(position, Lake))
            {
                SetLocal(-100f, position, state.Velocity);
                return;
            }
            SetLocal(depth, position, state.Velocity);
            Vector3 v = state.Velocity;
            LocalSwimming = SwimPredictionContextPatch.Local ? !state.IsOnGround && depth >= 0.2f : LocalSwimming;
            if (state.IsOnGround)
            {
                if (depth > SwimDepth)
                {
                    Vector3 along = Vector3.ProjectOnPlane(v, up);
                    state.Velocity = v - along * Mathf.Clamp01(0.45f * Mathf.Clamp01(depth / 2f));
                }
                return;
            }
            if (depth < 0.2f)
            {
                return;
            }

            float g = gravity.magnitude;
            Vector3 upDir = -gravity / g;
            float submerged = Mathf.Clamp01(depth / FloatDepth);
            v -= gravity * dt * Mathf.Lerp(0.6f, 0.97f, submerged);
            v *= Mathf.Exp(-2.2f * submerged * dt);

            Vector3 move = input.HorizontalMoveInput;
            Vector3 look = input.CameraRotation * move;
            bool diving = move.sqrMagnitude > 0.01f && Vector3.Dot(look.normalized, upDir) < -0.25f;
            float speed = input.SprintInput ? SprintSwimSpeed : SwimSpeed;
            Vector3 desired = Vector3.zero;
            bool steering = false;
            if (move.sqrMagnitude > 0.01f)
            {
                Vector3 dir = look.normalized;
                if (depth < FloatDepth && !diving)
                {
                    dir = Vector3.ProjectOnPlane(dir, upDir).normalized;
                }
                desired += dir * speed;
                steering = true;
            }
            if (input.VerticalInput > 0f)
            {
                desired += upDir * SurfaceSpeed;
                steering = true;
            }
            else if (input.VerticalInput < 0f)
            {
                desired -= upDir * SurfaceSpeed;
                steering = true;
            }
            if (steering)
            {
                v = Vector3.MoveTowards(v, desired, Acceleration * dt);
            }
            if (!diving && input.VerticalInput >= 0f && depth < FloatDepth + 0.6f)
            {
                float rise = Vector3.Dot(v, upDir);
                float target = (depth - FloatDepth) * 2.5f;
                v += upDir * (Mathf.MoveTowards(rise, target, 6f * dt) - rise);
            }
            state.Velocity = v;
            state.IsJumping = true;
        }

        private static void SetLocal(float depth, Vector3Double position, Vector3 velocity)
        {
            if (!SwimPredictionContextPatch.Local)
            {
                return;
            }
            LocalDepth = depth;
            LocalSpeed = velocity.magnitude;
            if (depth <= 0f) LocalSwimming = false;
            bool wet = depth > 0.1f;
            if (wet && !_localWet && Time.time - _lastSplash > 0.6f)
            {
                float down = -Vector3.Dot(velocity, (Vector3)position.Normalized);
                if (down > 3f)
                {
                    _lastSplash = Time.time;
                    Effects.Splashes.QueuePlayerSplash(position, down);
                }
            }
            _localWet = wet;
        }
    }
}
