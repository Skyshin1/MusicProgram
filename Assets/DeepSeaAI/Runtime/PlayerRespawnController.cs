using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace DeepSeaAI
{
    [DisallowMultipleComponent]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField] private Transform respawnPoint;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
        [SerializeField, Min(0f)] private float postRespawnProtection = 1f;

        private XROrigin xrOrigin;
        private Camera playerCamera;
        private CanvasGroup fadeGroup;
        private Vector3 fallbackCameraPosition;
        private Vector3 fallbackForward;
        private bool isRespawning;
        private float protectedUntil;

        public event Action Respawned;
        public bool IsProtected => isRespawning || Time.unscaledTime < protectedUntil;

        public void Configure(Transform point)
        {
            respawnPoint = point;
            CaptureFallbackPose();
        }

        private void Awake()
        {
            xrOrigin = GetComponentInParent<XROrigin>();
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();
            playerCamera = xrOrigin != null && xrOrigin.Camera != null
                ? xrOrigin.Camera
                : Camera.main;
            CaptureFallbackPose();
            EnsureFadeCanvas();
        }

        public bool Kill(Transform source)
        {
            return Kill(source != null ? source.position : transform.position);
        }

        public bool Kill(Vector3 sourcePosition)
        {
            if (IsProtected)
                return false;

            StartCoroutine(RespawnRoutine());
            return true;
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            List<Behaviour> disabledMovement = DisableMovement();
            yield return FadeTo(1f, fadeOutDuration);

            ReleaseHeldObjects();
            TeleportToRespawn();
            ClearPlayerVelocity();
            NoiseSystem.Emit(new NoiseStimulus(
                RespawnCameraPosition(),
                0f,
                NoiseKind.Interaction,
                transform,
                Time.time));
            Respawned?.Invoke();

            yield return null;
            yield return FadeTo(0f, fadeInDuration);
            RestoreMovement(disabledMovement);
            protectedUntil = Time.unscaledTime + postRespawnProtection;
            isRespawning = false;
        }

        private void CaptureFallbackPose()
        {
            Camera cameraToUse = playerCamera != null ? playerCamera : Camera.main;
            if (cameraToUse == null)
                return;

            fallbackCameraPosition = cameraToUse.transform.position;
            fallbackForward = Vector3.ProjectOnPlane(cameraToUse.transform.forward, Vector3.up).normalized;
            if (fallbackForward.sqrMagnitude < 0.001f)
                fallbackForward = Vector3.forward;
        }

        private void TeleportToRespawn()
        {
            Vector3 targetPosition = respawnPoint != null
                ? respawnPoint.position
                : fallbackCameraPosition;
            Vector3 targetForward = respawnPoint != null
                ? Vector3.ProjectOnPlane(respawnPoint.forward, Vector3.up).normalized
                : fallbackForward;
            if (targetForward.sqrMagnitude < 0.001f)
                targetForward = Vector3.forward;

            CharacterController controller = xrOrigin != null
                ? xrOrigin.GetComponentInParent<CharacterController>()
                : null;
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled)
                controller.enabled = false;

            if (xrOrigin != null)
            {
                xrOrigin.MoveCameraToWorldLocation(targetPosition);
                xrOrigin.MatchOriginUpCameraForward(Vector3.up, targetForward);
            }
            else
            {
                transform.SetPositionAndRotation(
                    targetPosition,
                    Quaternion.LookRotation(targetForward, Vector3.up));
            }

            if (controllerWasEnabled)
                controller.enabled = true;
        }

        private Vector3 RespawnCameraPosition()
        {
            if (playerCamera != null)
                return playerCamera.transform.position;
            return respawnPoint != null ? respawnPoint.position : fallbackCameraPosition;
        }

        private void ClearPlayerVelocity()
        {
            Transform root = xrOrigin != null ? xrOrigin.transform : transform;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null || body.isKinematic)
                    continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ReleaseHeldObjects()
        {
            Transform root = xrOrigin != null ? xrOrigin.transform : transform;
            foreach (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor in root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true))
            {
                XRInteractionManager manager = interactor.interactionManager;
                if (manager == null || interactor.interactablesSelected.Count == 0)
                    continue;

                var selected = new List<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>(interactor.interactablesSelected);
                foreach (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable in selected)
                {
                    if (interactable != null)
                        manager.SelectExit(interactor, interactable);
                }
            }
        }

        private List<Behaviour> DisableMovement()
        {
            var result = new List<Behaviour>();
            Transform root = xrOrigin != null ? xrOrigin.transform : transform;
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || !behaviour.enabled || behaviour == this)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (!typeName.Contains("MoveProvider") &&
                    !typeName.Contains("Locomotion") &&
                    !typeName.Contains("ContinuousMove"))
                {
                    continue;
                }

                behaviour.enabled = false;
                result.Add(behaviour);
            }
            return result;
        }

        private static void RestoreMovement(List<Behaviour> behaviours)
        {
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            EnsureFadeCanvas();
            float start = fadeGroup.alpha;
            if (duration <= 0.001f)
            {
                fadeGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeGroup.alpha = target;
        }

        private void EnsureFadeCanvas()
        {
            if (fadeGroup != null)
                return;

            if (playerCamera == null)
                playerCamera = Camera.main;

            var canvasObject = new GameObject("VR Death Fade Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = playerCamera;
            canvas.planeDistance = playerCamera != null
                ? Mathf.Max(playerCamera.nearClipPlane + 0.02f, 0.05f)
                : 0.1f;
            canvas.sortingOrder = short.MaxValue;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            fadeGroup = canvasObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;

            var imageObject = new GameObject("Black");
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }
    }
}
