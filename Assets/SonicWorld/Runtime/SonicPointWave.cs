using UnityEngine;

namespace SonicWorld
{
    /// <summary>
    /// Layered spectrum lines laid over an editable Catmull-Rom curve. Spatial
    /// sound events become deterministic pulses that travel away from the nearest
    /// point on the curve instead of continuously changing phase at their origin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SonicPointWave : MonoBehaviour
    {
        private struct Pulse
        {
            public bool Active;
            public bool OneWay;
            public float Origin;
            public float Age;
            public float Strength;
            public float Speed;
            public float Width;
            public float Decay;
            public float Phase;
            public Vector3 Bands;
        }

        private const int PulseCapacity = 48;
        private const int DenseMultiplier = 4;
        public const int MinimumControlPointCount = 4;
        public const int MaximumControlPointCount = 64;

        [SerializeField, Range(8, 48)] private int lineCount = 28;
        [SerializeField, Range(48, 512)] private int pointsPerLine = 160;
        [SerializeField, Range(0.5f, 30f)] private float height = 2.2f;
        [SerializeField, Min(0.1f)]
        [InspectorName("Overall Wave Width")]
        [Tooltip("Total side-to-side width of the complete layered wave, in metres.")]
        private float depth = 1.25f;
        [SerializeField, Range(0.002f, 0.6f)]
        [InspectorName("Wave Line Thickness")]
        [Tooltip("World-space thickness of every coloured wave line, in metres.")]
        private float lineWidth = 0.045f;
        [SerializeField]
        [InspectorName("Wave Color Gradient")]
        [Tooltip("Controls the wave colour and transparency from its start to its end.")]
        private Gradient waveColorGradient = CreateDefaultWaveGradient();
        [SerializeField, Range(0f, 0.25f)]
        [InspectorName("Color Variation Across Lines")]
        [Tooltip("Adds a small hue variation across the layered lines. Set to zero for the exact same gradient on every line.")]
        private float lineColorVariation = 0.08f;
        [SerializeField, Range(0.25f, 2f)] private float fullStrengthDistance = 0.75f;
        [SerializeField, Range(2f, 12f)] private float maximumReceiveDistance = 6f;
        [SerializeField, Range(0.5f, 8f)] private float pulseSpeed = 2.35f;
        [SerializeField, Range(0f, 2f)] private float pulseDecay = 0.48f;
        [SerializeField, Range(0f, 1f)]
        [InspectorName("Wave Calmness")]
        [Tooltip("Globally softens the attack, shape and motion of BGM, collision and swing waves.")]
        private float waveCalmness = 0.65f;
        [SerializeField]
        [InspectorName("Allow Runtime VR Point Editing")]
        [Tooltip("Allows VR Trigger interactions to move this wave's control points during Play Mode.")]
        private bool allowRuntimePointEditing = true;
        [SerializeField]
        [InspectorName("Closed Loop")]
        [Tooltip("Connects the final control point back to the first while keeping both as separate editable points.")]
        private bool closedLoop;
        [SerializeField]
        [InspectorName("Show Straight Control Polygon")]
        [Tooltip("Shows the straight helper lines between control points. These lines can have corners and are not the smooth wave curve.")]
        private bool showControlPolygon;
        [SerializeField, Range(0.02f, 5f)] private float bgmInterval = 0.14f;
        [SerializeField, Min(0f)]
        [Tooltip("Strength of BGM-generated visual pulses. Changes affect newly spawned pulses.")]
        private float bgmStrength = 0.18f;
        [SerializeField] private Material lineMaterial;
        [SerializeField, HideInInspector] private Transform[] controlPoints;

        private readonly Pulse[] pulses = new Pulse[PulseCapacity];
        private int nextPulse;
        private uint backgroundSequence;
        private float nextBgmTime;

        private LineRenderer[] lines;
        private LineRenderer controlPolygon;
        private Vector3[][] positions;
        private Vector3[][] targetPositions;
        private Vector3[] curvePoints;
        private Vector3[] curveNormals;
        private Vector3[] curveBinormals;
        private float[] curveDistances;
        private float[] curveParameters;
        private Vector3[] densePoints;
        private float[] denseDistances;
        private int[] affectedPointIndices;
        private float[] affectedPointAmplitudes;
        private float[] lineShapeScratch;
        private Vector3[] lastControlPositions;
        private Quaternion[] lastControlRotations;
        private Vector3[] controlPolygonPositions;
        private float curveLength = 1f;
        private float appliedLineWidth = -1f;
        private int builtLineCount = -1;
        private int builtPointsPerLine = -1;
        private bool hasRenderedFrame;
        private bool lastClosedLoop;
        private bool lastShowControlPolygon;
        private SonicAudioBus subscribedBus;

        public Transform[] ControlPoints => controlPoints;
        public Material LineMaterial => lineMaterial;
        public float MaximumReceiveDistance => maximumReceiveDistance;
        public bool AllowRuntimePointEditing => allowRuntimePointEditing;
        public bool ClosedLoop => closedLoop;

        public void Configure(Material material)
        {
            lineMaterial = material;
        }

        public void Configure(Material material, Transform[] points)
        {
            lineMaterial = material;
            SetControlPoints(points);
        }

        public bool SetControlPoints(Transform[] points)
        {
            if (points == null ||
                points.Length < MinimumControlPointCount ||
                points.Length > MaximumControlPointCount)
            {
                return false;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                    return false;
            }

            controlPoints = points;
            if (Application.isPlaying && lines != null)
            {
                AllocateCurveCaches();
                if (controlPolygon != null)
                    controlPolygon.positionCount = controlPoints.Length;
                RebuildCurve();
            }
            return true;
        }

        private void Awake()
        {
            EnsureWaveColorGradient();
            if (controlPoints == null ||
                controlPoints.Length < MinimumControlPointCount ||
                controlPoints.Length > MaximumControlPointCount)
            {
                CreateDefaultControlPoints();
            }

            BuildRenderers();
            RebuildCurve();
        }

        private void OnValidate()
        {
            EnsureWaveColorGradient();
            if (Application.isPlaying && lines != null)
                ApplyWaveColorGradient();
        }

        private void OnEnable()
        {
            EnsureBusSubscription();
        }

        private void OnDisable()
        {
            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = null;
        }

        private void Update()
        {
            EnsureBusSubscription();
            RebuildRenderersIfResolutionChanged();
            ApplyLineWidthIfChanged();
            if (CurveChanged())
                RebuildCurve();

            SpawnBackgroundPulse();
            UpdatePulses(Time.deltaTime);
            RenderWave();
        }

        private void RebuildRenderersIfResolutionChanged()
        {
            if (lines != null &&
                builtLineCount == lineCount &&
                builtPointsPerLine == pointsPerLine)
            {
                return;
            }

            DestroyGeneratedRenderers();
            BuildRenderers();
            RebuildCurve();
        }

        private void DestroyGeneratedRenderers()
        {
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] != null)
                        Destroy(lines[i].gameObject);
                }
            }

            if (controlPolygon != null)
                Destroy(controlPolygon.gameObject);

            lines = null;
            controlPolygon = null;
            positions = null;
            targetPositions = null;
            hasRenderedFrame = false;
            appliedLineWidth = -1f;
        }

        private void ApplyLineWidthIfChanged()
        {
            if (lines == null || Mathf.Approximately(appliedLineWidth, lineWidth))
                return;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null)
                    lines[i].widthMultiplier = lineWidth;
            }

            if (controlPolygon != null)
            {
                controlPolygon.widthMultiplier =
                    Mathf.Max(0.008f, lineWidth * 0.28f);
            }
            appliedLineWidth = lineWidth;
        }

        private void EnsureBusSubscription()
        {
            SonicAudioBus current = SonicAudioBus.Instance;
            if (current == subscribedBus)
                return;

            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = current;
            if (subscribedBus != null)
                subscribedBus.SoundEventReported += OnSoundEvent;
        }

        private void OnSoundEvent(SonicSoundEvent soundEvent)
        {
            if (curvePoints == null || curvePoints.Length == 0)
                return;

            int nearest = 0;
            float nearestSqrDistance = float.MaxValue;
            for (int i = 0; i < curvePoints.Length; i++)
            {
                Vector3 worldPoint = transform.TransformPoint(curvePoints[i]);
                float sqrDistance = (worldPoint - soundEvent.Position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearest = i;
                    nearestSqrDistance = sqrDistance;
                }
            }

            float distance = Mathf.Sqrt(nearestSqrDistance);
            if (distance > maximumReceiveDistance)
                return;

            float falloff = distance <= fullStrengthDistance
                ? 1f
                : 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        fullStrengthDistance,
                        maximumReceiveDistance,
                        distance));
            AddPulse(
                curveDistances[nearest],
                soundEvent.Strength * falloff,
                soundEvent.Bands,
                soundEvent.Sequence,
                false);
        }

        private void SpawnBackgroundPulse()
        {
            SonicMusicPlayer music = SonicMusicPlayer.Instance;
            if (music == null || !music.IsPlaying || Time.time < nextBgmTime)
                return;

            nextBgmTime = Time.time + bgmInterval;
            float energy = music.CurrentEnergy;
            if (energy < 0.01f)
                return;

            Vector3 bands = music.CurrentBands;
            if (bands.sqrMagnitude < 0.0001f)
                bands = new Vector3(0.5f, 0.35f, 0.15f);
            AddPulse(
                0f,
                energy * bgmStrength,
                bands,
                ++backgroundSequence + 100000u,
                true);
        }

        private void AddPulse(
            float originDistance,
            float strength,
            Vector3 bands,
            uint sequence,
            bool oneWay)
        {
            if (strength <= 0.001f)
                return;

            float bandTotal = Mathf.Max(0.0001f, bands.x + bands.y + bands.z);
            bands /= bandTotal;
            pulses[nextPulse] = new Pulse
            {
                Active = true,
                OneWay = oneWay,
                Origin = Mathf.Clamp01(originDistance / Mathf.Max(0.001f, curveLength)),
                Age = 0f,
                Strength = Mathf.Clamp01(strength),
                Speed = pulseSpeed * Mathf.Lerp(0.82f, 1.22f, bands.z),
                Width = Mathf.Lerp(0.42f, 0.18f, bands.z),
                Decay = pulseDecay * Mathf.Lerp(0.82f, 1.28f, bands.z),
                Phase = sequence * 2.39996323f,
                Bands = bands
            };
            nextPulse = (nextPulse + 1) % PulseCapacity;
        }

        private void UpdatePulses(float deltaTime)
        {
            for (int i = 0; i < pulses.Length; i++)
            {
                if (!pulses[i].Active)
                    continue;

                Pulse pulse = pulses[i];
                pulse.Age += deltaTime;
                float originDistance = pulse.Origin * curveLength;
                float maximumTravel = closedLoop
                    ? (pulse.OneWay ? curveLength : curveLength * 0.5f)
                    : pulse.OneWay
                        ? curveLength - originDistance
                        : Mathf.Max(originDistance, curveLength - originDistance);
                float duration = maximumTravel / Mathf.Max(0.01f, pulse.Speed) + 2.5f;
                if (pulse.Age > duration ||
                    pulse.Strength * Mathf.Exp(-pulse.Decay * pulse.Age) < 0.002f)
                {
                    pulse.Active = false;
                }
                pulses[i] = pulse;
            }
        }

        private void RenderWave()
        {
            float calmness = Mathf.Clamp01(waveCalmness);
            float spatialSoftness = Mathf.Lerp(1f, 2.8f, calmness);
            float attackDuration = Mathf.Lerp(0.015f, 0.55f, calmness);
            float middleBandScale = Mathf.Lerp(1f, 0.72f, calmness);
            float highBandScale = Mathf.Lerp(1f, 0.38f, calmness);

            for (int line = 0; line < lineCount; line++)
            {
                float lineT = lineCount > 1 ? line / (float)(lineCount - 1) : 0.5f;
                float layer = lineT - 0.5f;
                for (int point = 0; point < pointsPerLine; point++)
                {
                    targetPositions[line][point] =
                        curvePoints[point] +
                        curveBinormals[point] * (layer * depth);
                }
            }

            // The pulse envelope only depends on the point along the curve, while
            // the spectrum shape only depends on the line. Calculate both once
            // per pulse and reuse them instead of repeating expensive Exp/Sin
            // operations for every line-point pair.
            for (int pulseIndex = 0; pulseIndex < pulses.Length; pulseIndex++)
            {
                Pulse pulse = pulses[pulseIndex];
                if (!pulse.Active)
                    continue;

                float front = pulse.Speed * pulse.Age;
                float attack = Mathf.SmoothStep(
                    0f,
                    1f,
                    pulse.Age / Mathf.Max(0.001f, attackDuration));
                float ageDecay =
                    Mathf.Exp(-pulse.Decay * pulse.Age) * attack;
                float inverseWidth =
                    1f / Mathf.Max(0.02f, pulse.Width * spatialSoftness);
                int affectedCount = 0;
                for (int point = 0; point < pointsPerLine; point++)
                {
                    float signedDistance =
                        curveDistances[point] - pulse.Origin * curveLength;
                    float distance;
                    if (closedLoop)
                    {
                        float forwardDistance = Mathf.Repeat(
                            signedDistance,
                            curveLength);
                        distance = pulse.OneWay
                            ? forwardDistance
                            : Mathf.Min(
                                forwardDistance,
                                curveLength - forwardDistance);
                    }
                    else
                    {
                        if (pulse.OneWay && signedDistance < 0f)
                            continue;
                        distance = pulse.OneWay
                            ? signedDistance
                            : Mathf.Abs(signedDistance);
                    }
                    float offset = (distance - front) * inverseWidth;
                    float envelope =
                        Mathf.Exp(-offset * offset * 1.7f) * ageDecay;
                    if (envelope < 0.0005f)
                        continue;

                    affectedPointIndices[affectedCount] = point;
                    affectedPointAmplitudes[affectedCount] =
                        height * pulse.Strength * envelope;
                    affectedCount++;
                }

                if (affectedCount == 0)
                    continue;

                for (int line = 0; line < lineCount; line++)
                {
                    float lineT =
                        lineCount > 1 ? line / (float)(lineCount - 1) : 0.5f;
                    lineShapeScratch[line] =
                        Mathf.Sin(lineT * Mathf.PI * 2f + pulse.Phase) *
                            pulse.Bands.x * 1.05f +
                        Mathf.Sin(lineT * Mathf.PI * 5f + pulse.Phase * 0.71f) *
                            pulse.Bands.y * 0.82f * middleBandScale +
                        Mathf.Sin(lineT * Mathf.PI * 9f - pulse.Phase * 0.53f) *
                            pulse.Bands.z * 0.66f * highBandScale;
                }

                for (int line = 0; line < lineCount; line++)
                {
                    float lineShape = lineShapeScratch[line];
                    for (int affected = 0; affected < affectedCount; affected++)
                    {
                        int point = affectedPointIndices[affected];
                        targetPositions[line][point] +=
                            curveNormals[point] *
                            (affectedPointAmplitudes[affected] * lineShape);
                    }
                }
            }

            float smoothingTime = Mathf.Lerp(0f, 0.48f, calmness);
            float blend = !hasRenderedFrame || smoothingTime <= 0.001f
                ? 1f
                : 1f - Mathf.Exp(
                    -Mathf.Max(0f, Time.deltaTime) / smoothingTime);
            for (int line = 0; line < lineCount; line++)
            {
                for (int point = 0; point < pointsPerLine; point++)
                {
                    positions[line][point] = Vector3.LerpUnclamped(
                        positions[line][point],
                        targetPositions[line][point],
                        blend);
                }
                lines[line].SetPositions(positions[line]);
            }
            hasRenderedFrame = true;
        }

        private void BuildRenderers()
        {
            AllocateCurveCaches();

            lines = new LineRenderer[lineCount];
            positions = new Vector3[lineCount][];
            targetPositions = new Vector3[lineCount][];
            for (int line = 0; line < lineCount; line++)
            {
                GameObject lineObject = new GameObject($"Spectrum Line {line + 1:00}");
                lineObject.layer = gameObject.layer;
                lineObject.transform.SetParent(transform, false);
                LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = false;
                renderer.positionCount = pointsPerLine;
                renderer.loop = closedLoop;
                renderer.widthMultiplier = lineWidth;
                renderer.numCornerVertices = 1;
                renderer.numCapVertices = 1;
                renderer.alignment = LineAlignment.View;
                renderer.textureMode = LineTextureMode.Stretch;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = lineMaterial;
                renderer.colorGradient = BuildWaveGradient(
                    lineCount > 1 ? line / (float)(lineCount - 1) : 0.5f);
                lines[line] = renderer;
                positions[line] = new Vector3[pointsPerLine];
                targetPositions[line] = new Vector3[pointsPerLine];
            }
            hasRenderedFrame = false;
            appliedLineWidth = lineWidth;

            GameObject polygonObject = new GameObject("Control Polygon");
            polygonObject.layer = gameObject.layer;
            polygonObject.transform.SetParent(transform, false);
            controlPolygon = polygonObject.AddComponent<LineRenderer>();
            controlPolygon.useWorldSpace = false;
            controlPolygon.positionCount = controlPoints.Length;
            controlPolygon.loop = closedLoop;
            controlPolygon.enabled = showControlPolygon;
            controlPolygon.widthMultiplier = Mathf.Max(0.008f, lineWidth * 0.28f);
            controlPolygon.numCornerVertices = 2;
            controlPolygon.numCapVertices = 2;
            controlPolygon.sharedMaterial = lineMaterial;
            controlPolygon.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            controlPolygon.receiveShadows = false;
            controlPolygon.colorGradient = BuildControlGradient();
            builtLineCount = lineCount;
            builtPointsPerLine = pointsPerLine;
        }

        private void AllocateCurveCaches()
        {
            curvePoints = new Vector3[pointsPerLine];
            curveNormals = new Vector3[pointsPerLine];
            curveBinormals = new Vector3[pointsPerLine];
            curveDistances = new float[pointsPerLine];
            curveParameters = new float[pointsPerLine];
            int denseCount = Mathf.Max(pointsPerLine * DenseMultiplier, controlPoints.Length * 12);
            densePoints = new Vector3[denseCount];
            denseDistances = new float[denseCount];
            affectedPointIndices = new int[pointsPerLine];
            affectedPointAmplitudes = new float[pointsPerLine];
            lineShapeScratch = new float[lineCount];
            lastControlPositions = new Vector3[controlPoints.Length];
            lastControlRotations = new Quaternion[controlPoints.Length];
            controlPolygonPositions = new Vector3[controlPoints.Length];
        }

        private bool CurveChanged()
        {
            if (closedLoop != lastClosedLoop ||
                showControlPolygon != lastShowControlPolygon)
            {
                return true;
            }

            if (controlPoints == null || controlPoints.Length != lastControlPositions.Length)
                return true;

            for (int i = 0; i < controlPoints.Length; i++)
            {
                if (controlPoints[i] == null ||
                    (controlPoints[i].localPosition - lastControlPositions[i]).sqrMagnitude >
                    0.0000001f ||
                    Quaternion.Angle(
                        controlPoints[i].localRotation,
                        lastControlRotations[i]) > 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        private void RebuildCurve()
        {
            if (controlPoints == null ||
                controlPoints.Length < MinimumControlPointCount)
                return;

            int denseCount = densePoints.Length;
            for (int i = 0; i < denseCount; i++)
            {
                float normalized = i / (float)(denseCount - 1);
                densePoints[i] = EvaluateCurve(normalized);
                denseDistances[i] = i == 0
                    ? 0f
                    : denseDistances[i - 1] +
                      Vector3.Distance(densePoints[i - 1], densePoints[i]);
            }

            curveLength = Mathf.Max(0.001f, denseDistances[denseCount - 1]);
            int denseIndex = 1;
            for (int point = 0; point < pointsPerLine; point++)
            {
                float targetDistance =
                    curveLength * point /
                    Mathf.Max(
                        1f,
                        closedLoop ? pointsPerLine : pointsPerLine - 1f);
                while (denseIndex < denseCount - 1 &&
                       denseDistances[denseIndex] < targetDistance)
                {
                    denseIndex++;
                }

                int previous = Mathf.Max(0, denseIndex - 1);
                float segmentLength =
                    denseDistances[denseIndex] - denseDistances[previous];
                float blend = segmentLength > 0.00001f
                    ? (targetDistance - denseDistances[previous]) / segmentLength
                    : 0f;
                curvePoints[point] = Vector3.Lerp(
                    densePoints[previous],
                    densePoints[denseIndex],
                    blend);
                curveDistances[point] = targetDistance;
                curveParameters[point] = Mathf.Lerp(
                    previous / (float)(denseCount - 1),
                    denseIndex / (float)(denseCount - 1),
                    blend);
            }

            BuildFrames();
            if (lines != null)
            {
                for (int line = 0; line < lines.Length; line++)
                {
                    if (lines[line] != null)
                        lines[line].loop = closedLoop;
                }
            }
            if (controlPolygon != null)
            {
                controlPolygon.loop = closedLoop;
                controlPolygon.enabled = showControlPolygon;
            }
            for (int i = 0; i < controlPoints.Length; i++)
            {
                lastControlPositions[i] = controlPoints[i].localPosition;
                lastControlRotations[i] = controlPoints[i].localRotation;
                controlPolygonPositions[i] = controlPoints[i].localPosition;
            }
            controlPolygon.SetPositions(controlPolygonPositions);
            lastClosedLoop = closedLoop;
            lastShowControlPolygon = showControlPolygon;
        }

        private void BuildFrames()
        {
            Vector3 previousNormal = Vector3.up;
            for (int point = 0; point < pointsPerLine; point++)
            {
                Vector3 tangent = GetTangent(point);
                Vector3 normal =
                    EvaluateControlNormal(curveParameters[point]);
                normal -= tangent * Vector3.Dot(normal, tangent);
                if (normal.sqrMagnitude < 0.0001f)
                {
                    normal = previousNormal -
                        tangent * Vector3.Dot(previousNormal, tangent);
                }
                if (normal.sqrMagnitude < 0.0001f)
                {
                    normal = Vector3.forward -
                        tangent * Vector3.Dot(Vector3.forward, tangent);
                }
                normal.Normalize();
                if (point > 0 && Vector3.Dot(normal, previousNormal) < 0f)
                    normal = -normal;
                curveNormals[point] = normal;
                curveBinormals[point] = Vector3.Cross(tangent, normal).normalized;
                previousNormal = normal;
            }
        }

        private Vector3 GetTangent(int point)
        {
            int previous = closedLoop
                ? (point - 1 + pointsPerLine) % pointsPerLine
                : Mathf.Max(0, point - 1);
            int next = closedLoop
                ? (point + 1) % pointsPerLine
                : Mathf.Min(pointsPerLine - 1, point + 1);
            Vector3 tangent = curvePoints[next] - curvePoints[previous];
            return tangent.sqrMagnitude > 0.000001f
                ? tangent.normalized
                : Vector3.right;
        }

        private Vector3 EvaluateCurve(float normalized)
        {
            if (closedLoop)
            {
                int pointCount = controlPoints.Length;
                float scaledLoop = Mathf.Clamp01(normalized) * pointCount;
                int rawSegment = Mathf.FloorToInt(scaledLoop);
                int loopSegment = rawSegment % pointCount;
                float loopT = scaledLoop - rawSegment;
                Vector3 loopP0 =
                    controlPoints[(loopSegment - 1 + pointCount) % pointCount]
                        .localPosition;
                Vector3 loopP1 = controlPoints[loopSegment].localPosition;
                Vector3 loopP2 =
                    controlPoints[(loopSegment + 1) % pointCount].localPosition;
                Vector3 loopP3 =
                    controlPoints[(loopSegment + 2) % pointCount].localPosition;
                return EvaluateCatmullRom(
                    loopP0,
                    loopP1,
                    loopP2,
                    loopP3,
                    loopT);
            }

            int segmentCount = controlPoints.Length - 1;
            float scaled = Mathf.Clamp01(normalized) * segmentCount;
            int segment = Mathf.Min(Mathf.FloorToInt(scaled), segmentCount - 1);
            float t = scaled - segment;
            Vector3 p0 = controlPoints[Mathf.Max(0, segment - 1)].localPosition;
            Vector3 p1 = controlPoints[segment].localPosition;
            Vector3 p2 = controlPoints[Mathf.Min(segment + 1, controlPoints.Length - 1)]
                .localPosition;
            Vector3 p3 = controlPoints[Mathf.Min(segment + 2, controlPoints.Length - 1)]
                .localPosition;
            return EvaluateCatmullRom(p0, p1, p2, p3, t);
        }

        private static Vector3 EvaluateCatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f *
                ((2f * p1) +
                 (-p0 + p2) * t +
                 (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                 (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 EvaluateControlNormal(float normalized)
        {
            if (closedLoop)
            {
                int pointCount = controlPoints.Length;
                float scaledLoop = Mathf.Clamp01(normalized) * pointCount;
                int rawSegment = Mathf.FloorToInt(scaledLoop);
                int loopSegment = rawSegment % pointCount;
                float tLoop = scaledLoop - rawSegment;
                return EvaluateControlNormalSegment(
                    (loopSegment - 1 + pointCount) % pointCount,
                    loopSegment,
                    (loopSegment + 1) % pointCount,
                    (loopSegment + 2) % pointCount,
                    tLoop);
            }

            int segmentCount = controlPoints.Length - 1;
            float scaled = Mathf.Clamp01(normalized) * segmentCount;
            int segment = Mathf.Min(
                Mathf.FloorToInt(scaled),
                segmentCount - 1);
            float t = scaled - segment;
            return EvaluateControlNormalSegment(
                Mathf.Max(0, segment - 1),
                segment,
                Mathf.Min(segment + 1, controlPoints.Length - 1),
                Mathf.Min(segment + 2, controlPoints.Length - 1),
                t);
        }

        private Vector3 EvaluateControlNormalSegment(
            int index0,
            int index1,
            int index2,
            int index3,
            float t)
        {
            Vector3 normal0 =
                controlPoints[index0].localRotation * Vector3.up;
            Vector3 normal1 =
                controlPoints[index1].localRotation * Vector3.up;
            Vector3 normal2 =
                controlPoints[index2].localRotation * Vector3.up;
            Vector3 normal3 =
                controlPoints[index3].localRotation * Vector3.up;
            Vector3 smoothed = EvaluateCatmullRom(
                normal0,
                normal1,
                normal2,
                normal3,
                t);
            if (smoothed.sqrMagnitude < 0.0001f)
                smoothed = Vector3.Slerp(normal1, normal2, t);
            return smoothed.sqrMagnitude > 0.0001f
                ? smoothed.normalized
                : Vector3.up;
        }

        private void CreateDefaultControlPoints()
        {
            controlPoints = new Transform[6];
            for (int i = 0; i < controlPoints.Length; i++)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = $"Curve Control {i + 1:00}";
                point.transform.SetParent(transform, false);
                float t = i / (float)(controlPoints.Length - 1);
                point.transform.localPosition = new Vector3(
                    Mathf.Lerp(-6f, 6f, t),
                    Mathf.Sin(t * Mathf.PI * 2f) * 0.55f,
                    Mathf.Sin(t * Mathf.PI) * 0.65f);
                point.transform.localScale = Vector3.one * 0.1f;
                point.GetComponent<Renderer>().sharedMaterial = lineMaterial;
                point.AddComponent<SonicCurveControlPoint>();
                controlPoints[i] = point.transform;
            }
        }

        private void EnsureWaveColorGradient()
        {
            if (waveColorGradient == null)
                waveColorGradient = CreateDefaultWaveGradient();
        }

        private void ApplyWaveColorGradient()
        {
            EnsureWaveColorGradient();
            if (lines == null)
                return;

            for (int line = 0; line < lines.Length; line++)
            {
                if (lines[line] == null)
                    continue;
                float layer = lines.Length > 1
                    ? line / (float)(lines.Length - 1)
                    : 0.5f;
                lines[line].colorGradient = BuildWaveGradient(layer);
            }
        }

        private Gradient BuildWaveGradient(float layer)
        {
            EnsureWaveColorGradient();
            float hueOffset = (layer - 0.5f) * lineColorVariation;
            GradientColorKey[] colorKeys = waveColorGradient.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                Color.RGBToHSV(
                    colorKeys[i].color,
                    out float hue,
                    out float saturation,
                    out float value);
                colorKeys[i].color = Color.HSVToRGB(
                    Mathf.Repeat(hue + hueOffset, 1f),
                    saturation,
                    value,
                    true);
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                colorKeys,
                waveColorGradient.alphaKeys);
            gradient.mode = waveColorGradient.mode;
            return gradient;
        }

        private static Gradient CreateDefaultWaveGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.HSVToRGB(0.61f, 0.9f, 1f), 0f),
                    new GradientColorKey(Color.HSVToRGB(0.52f, 0.9f, 1f), 0.2f),
                    new GradientColorKey(Color.HSVToRGB(0.34f, 0.88f, 1f), 0.42f),
                    new GradientColorKey(Color.HSVToRGB(0.16f, 0.9f, 1f), 0.64f),
                    new GradientColorKey(Color.HSVToRGB(0.91f, 0.82f, 1f), 0.82f),
                    new GradientColorKey(Color.HSVToRGB(0.04f, 0.92f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.72f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(1f, 0.82f),
                    new GradientAlphaKey(0.72f, 1f)
                });
            return gradient;
        }

        private static Gradient BuildControlGradient()
        {
            Gradient gradient = new Gradient();
            Color cyan = new Color(0.05f, 0.92f, 1f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(cyan, 0f),
                    new GradientColorKey(cyan, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.28f, 0f),
                    new GradientAlphaKey(0.28f, 1f)
                });
            return gradient;
        }

        private void OnDrawGizmos()
        {
            if (controlPoints == null ||
                controlPoints.Length < MinimumControlPointCount)
                return;

            const int previewSteps = 96;
            Vector3 previousLocal = EvaluateCurve(0f);
            Vector3 previousTangent =
                GetPreviewTangent(0f, 1f / previewSteps);
            GetPreviewFrame(
                0f,
                previousTangent,
                out Vector3 previousNormal,
                out Vector3 previousBinormal);
            Vector3 previous = transform.TransformPoint(previousLocal);
            Vector3 previousLeft = transform.TransformPoint(
                previousLocal - previousBinormal * (depth * 0.5f));
            Vector3 previousRight = transform.TransformPoint(
                previousLocal + previousBinormal * (depth * 0.5f));

            for (int i = 1; i <= previewSteps; i++)
            {
                float normalized = i / (float)previewSteps;
                Vector3 currentLocal = EvaluateCurve(normalized);
                Vector3 tangent =
                    GetPreviewTangent(normalized, 1f / previewSteps);
                GetPreviewFrame(
                    normalized,
                    tangent,
                    out Vector3 normal,
                    out Vector3 binormal);
                Vector3 current = transform.TransformPoint(currentLocal);
                Vector3 currentLeft = transform.TransformPoint(
                    currentLocal - binormal * (depth * 0.5f));
                Vector3 currentRight = transform.TransformPoint(
                    currentLocal + binormal * (depth * 0.5f));

                Gizmos.color = GetPreviewWaveColor(normalized, 0.78f);
                Gizmos.DrawLine(previous, current);
                Gizmos.color = GetPreviewWaveColor(normalized, 0.5f);
                Gizmos.DrawLine(previousLeft, currentLeft);
                Gizmos.DrawLine(previousRight, currentRight);
                if (i % 8 == 0)
                {
                    Gizmos.color = GetPreviewWaveColor(normalized, 0.3f);
                    Gizmos.DrawLine(currentLeft, currentRight);
                    Gizmos.color = new Color(1f, 0.12f, 0.72f, 0.75f);
                    Gizmos.DrawLine(
                        current,
                        transform.TransformPoint(
                            currentLocal + normal *
                            Mathf.Min(1.5f, Mathf.Max(0.3f, height * 0.16f))));
                }

                previous = current;
                previousLeft = currentLeft;
                previousRight = currentRight;
            }
        }

        private Color GetPreviewWaveColor(
            float normalized,
            float alphaMultiplier)
        {
            EnsureWaveColorGradient();
            Color color = waveColorGradient.Evaluate(normalized);
            color.a *= alphaMultiplier;
            return color;
        }

        private Vector3 GetPreviewTangent(float normalized, float step)
        {
            float previousSample = closedLoop
                ? Mathf.Repeat(normalized - step, 1f)
                : Mathf.Max(0f, normalized - step);
            float nextSample = closedLoop
                ? Mathf.Repeat(normalized + step, 1f)
                : Mathf.Min(1f, normalized + step);
            Vector3 tangent =
                EvaluateCurve(nextSample) - EvaluateCurve(previousSample);
            return tangent.sqrMagnitude > 0.000001f
                ? tangent.normalized
                : Vector3.right;
        }

        private void OnDrawGizmosSelected()
        {
            if (controlPoints == null ||
                controlPoints.Length < MinimumControlPointCount)
                return;
            Gizmos.color = new Color(0.05f, 0.95f, 1f, 0.16f);
            Gizmos.DrawWireSphere(controlPoints[0].position, maximumReceiveDistance);
            Gizmos.DrawWireSphere(
                controlPoints[controlPoints.Length / 2].position,
                maximumReceiveDistance);
            Gizmos.DrawWireSphere(
                controlPoints[controlPoints.Length - 1].position,
                maximumReceiveDistance);
        }

        private void GetPreviewFrame(
            float normalized,
            Vector3 tangent,
            out Vector3 normal,
            out Vector3 binormal)
        {
            normal = EvaluateControlNormal(normalized);
            normal -= tangent * Vector3.Dot(normal, tangent);
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.forward -
                    tangent * Vector3.Dot(Vector3.forward, tangent);
            }
            normal.Normalize();
            binormal = Vector3.Cross(tangent, normal).normalized;
        }
    }
}
