using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Walden
{
    /// <summary>A deliberately steady first-person view for a quiet walking game.</summary>
    public sealed class WaldenPlayer : MonoBehaviour
    {
        public Camera View { get; private set; }
        public bool IsSeated { get; private set; }
        public float Sensitivity { get; set; } = 0.12f;

        private CharacterController controller;
        private Transform heldCup;
        private Transform heldMeal;
        private Vector3 safePosition;
        private float safeYaw;
        private Vector3 standingPosition;
        private float standingYaw;
        private float standingPitch;
        private float seatYaw;
        private float yaw;
        private float pitch;
        private float verticalSpeed;
        private bool lookWasActive;
        private Material ceramic;
        private Material tea;
        private Material soup;
        private Material carrot;
        private Material leafyGreen;
        private Mesh bowlMesh;

        public void Initialize(Vector3 position, float facingYaw)
        {
            // Ignore Raycast prevents the interaction ray from hitting our own capsule.
            gameObject.layer = 2;
            controller = GetComponent<CharacterController>();
            if (controller == null) controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.32f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.025f;
            controller.minMoveDistance = 0f;

            if (View == null)
            {
                var cameraObject = new GameObject("First-person view");
                cameraObject.transform.SetParent(transform, false);
                View = cameraObject.AddComponent<Camera>();
                View.tag = "MainCamera";
                View.fieldOfView = 68f;
                View.nearClipPlane = 0.06f;
                View.farClipPlane = 350f;
                View.allowHDR = true;
                View.allowMSAA = true;
                cameraObject.AddComponent<AudioListener>();
            }

            safePosition = position;
            safeYaw = facingYaw;
            IsSeated = false;
            lookWasActive = false;
            Teleport(position, facingYaw);
            SetHeldCup(false);
            SetHeldMeal(false);
        }

        // The game owns the update order and cursor/menu state.
        public void Tick(bool canControl)
        {
            bool lookActive = canControl && Cursor.lockState == CursorLockMode.Locked;
            bool acceptMouseDelta = lookActive && lookWasActive;
            lookWasActive = lookActive;
            if (!canControl || controller == null || View == null) return;

            Mouse mouse = Mouse.current;
            // Locking the cursor can produce a large synthetic delta. Discard the first
            // look sample after starting, resuming, or relocking, while movement stays responsive.
            if (mouse != null && acceptMouseDelta)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * Mathf.Clamp(Sensitivity, 0.025f, 0.4f);
                float pitchLimit = IsSeated ? 70f : 80f;
                pitch = Mathf.Clamp(pitch - delta.y * Mathf.Clamp(Sensitivity, 0.025f, 0.4f),
                    -pitchLimit, pitchLimit);
                if (IsSeated)
                    yaw = seatYaw + Mathf.Clamp(Mathf.DeltaAngle(seatYaw, yaw), -100f, 100f);
                ApplyView();
            }

            if (IsSeated) return;

            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            bool fastWalk = false;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                fastWalk = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 movement = transform.right * input.x + transform.forward * input.y;
            movement *= fastWalk ? 4.6f : 2.8f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(-28f, verticalSpeed - 20f * Time.deltaTime);
            movement.y = verticalSpeed;
            controller.Move(movement * Time.deltaTime);

            if (transform.position.y < -5f) Teleport(safePosition, safeYaw);
        }

        public void Sit(Vector3 position, float facingYaw)
        {
            if (IsSeated) return;
            standingPosition = transform.position;
            standingYaw = yaw;
            standingPitch = pitch;
            seatYaw = facingYaw;
            IsSeated = true;
            Teleport(position, facingYaw);
        }

        public void Stand()
        {
            if (!IsSeated) return;
            IsSeated = false;
            Teleport(standingPosition, standingYaw, standingPitch);
        }

        public void Teleport(Vector3 position, float facingYaw, float facingPitch = 0f)
        {
            bool hasController = controller != null;
            if (hasController) controller.enabled = false;
            transform.position = position;
            yaw = facingYaw;
            pitch = Mathf.Clamp(facingPitch, -80f, 80f);
            verticalSpeed = -2f;
            ApplyView();
            // While seated the capsule is disabled, so it cannot push off a chair.
            if (hasController) controller.enabled = !IsSeated;
        }

        private void ApplyView()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (View == null) return;
            View.transform.localPosition = new Vector3(0f, IsSeated ? 1.15f : 1.65f, 0f);
            View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SetHeldCup(bool visible)
        {
            if (View == null) return;
            if (heldCup == null && visible) BuildCup();
            if (heldCup != null) heldCup.gameObject.SetActive(visible);
        }

        public void SetHeldMeal(bool visible)
        {
            if (View == null) return;
            if (heldMeal == null && visible) BuildMeal();
            if (heldMeal != null) heldMeal.gameObject.SetActive(visible);
        }

        private void BuildCup()
        {
            heldCup = new GameObject("A warm cup of tea").transform;
            heldCup.SetParent(View.transform, false);
            heldCup.localPosition = new Vector3(0.28f, -0.24f, 0.46f);
            heldCup.localRotation = Quaternion.Euler(8f, -12f, -9f);
            EnsureCeramic();
            tea = MakeMaterial("Amber tea", new Color(0.22f, 0.11f, 0.035f), 0.6f);

            CupPart("Cup", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f),
                new Vector3(0.14f, 0.064f, 0.14f), ceramic);
            CupPart("Tea surface", PrimitiveType.Cylinder, new Vector3(0f, 0.0645f, 0f),
                new Vector3(0.122f, 0.001f, 0.122f), tea);

            // A small curved handle, assembled from short pieces without extra assets.
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, i / 8f);
                float b = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (i + 1) / 8f);
                Vector3 p = new Vector3(0.065f + Mathf.Cos(a) * 0.044f, Mathf.Sin(a) * 0.043f, 0f);
                Vector3 q = new Vector3(0.065f + Mathf.Cos(b) * 0.044f, Mathf.Sin(b) * 0.043f, 0f);
                Transform piece = CupPart("Handle", PrimitiveType.Cylinder, (p + q) * 0.5f,
                    new Vector3(0.012f, Vector3.Distance(p, q) * 0.5f, 0.012f), ceramic);
                piece.localRotation = Quaternion.FromToRotation(Vector3.up, q - p);
            }
        }

        private void BuildMeal()
        {
            heldMeal = new GameObject("A bowl of homemade soup").transform;
            heldMeal.SetParent(View.transform, false);
            heldMeal.localPosition = new Vector3(0.23f, -0.24f, 0.49f);
            heldMeal.localRotation = Quaternion.Euler(9f, -10f, -4f);
            EnsureCeramic();
            soup = MakeMaterial("Golden vegetable broth", new Color(0.48f, 0.29f, 0.10f), 0.58f);
            carrot = MakeMaterial("Carrot pieces", new Color(0.78f, 0.36f, 0.09f), 0.13f);
            leafyGreen = MakeMaterial("Garden greens", new Color(0.23f, 0.38f, 0.13f), 0.10f);

            // A shallow, open stoneware bowl: outside, rounded lip, and inner wall.
            const int sides = 32;
            float[] radii = { 0.055f, 0.14f, 0.125f, 0.055f };
            float[] heights = { -0.075f, 0.03f, 0.03f, -0.055f };
            var vertices = new Vector3[sides * radii.Length];
            var triangles = new int[sides * (radii.Length - 1) * 6];
            for (int ring = 0; ring < radii.Length; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    vertices[ring * sides + i] = new Vector3(Mathf.Cos(angle) * radii[ring],
                        heights[ring], Mathf.Sin(angle) * radii[ring]);
                }
            int index = 0;
            for (int ring = 0; ring < radii.Length - 1; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int a = ring * sides + i;
                    int b = ring * sides + (i + 1) % sides;
                    int c = a + sides;
                    int d = b + sides;
                    triangles[index++] = a; triangles[index++] = c; triangles[index++] = b;
                    triangles[index++] = b; triangles[index++] = c; triangles[index++] = d;
                }
            bowlMesh = new Mesh { name = "Hand-thrown soup bowl" };
            bowlMesh.vertices = vertices;
            bowlMesh.triangles = triangles;
            bowlMesh.RecalculateNormals();
            bowlMesh.RecalculateBounds();
            var bowl = new GameObject("Stoneware bowl") { layer = 2 };
            bowl.transform.SetParent(heldMeal, false);
            bowl.AddComponent<MeshFilter>().sharedMesh = bowlMesh;
            Renderer bowlRenderer = bowl.AddComponent<MeshRenderer>();
            bowlRenderer.sharedMaterial = ceramic;
            bowlRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bowlRenderer.receiveShadows = false;

            HeldPart(heldMeal, "Warm soup", PrimitiveType.Cylinder, new Vector3(0f, 0.013f, 0f),
                new Vector3(0.235f, 0.001f, 0.235f), soup);
            Vector3[] pieces =
            {
                new Vector3(-0.045f, 0.021f, -0.025f), new Vector3(0.037f, 0.022f, 0.028f),
                new Vector3(-0.018f, 0.021f, 0.060f), new Vector3(0.057f, 0.021f, -0.042f),
                new Vector3(-0.066f, 0.021f, 0.034f)
            };
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform piece = HeldPart(heldMeal, i % 2 == 0 ? "Carrot" : "Leafy vegetable",
                    PrimitiveType.Cube, pieces[i], new Vector3(0.031f, 0.014f, 0.023f),
                    i % 2 == 0 ? carrot : leafyGreen);
                piece.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
            }
        }

        private void EnsureCeramic()
        {
            if (ceramic == null)
                ceramic = MakeMaterial("Glazed stoneware", new Color(0.69f, 0.65f, 0.52f), 0.28f);
        }

        private Transform CupPart(string partName, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material)
        {
            return HeldPart(heldCup, partName, primitive, position, scale, material);
        }

        private Transform HeldPart(Transform parent, string partName, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        private static Material MakeMaterial(string materialName, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = materialName };
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private void OnDestroy()
        {
            if (ceramic != null) Destroy(ceramic);
            if (tea != null) Destroy(tea);
            if (soup != null) Destroy(soup);
            if (carrot != null) Destroy(carrot);
            if (leafyGreen != null) Destroy(leafyGreen);
            if (bowlMesh != null) Destroy(bowlMesh);
        }
    }
}
