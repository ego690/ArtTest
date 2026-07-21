using System.Collections.Generic;
using UnityEngine;

namespace StylizedGrassPrototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class StylizedGrassInteractor : MonoBehaviour
    {
        const int MaxInteractors = 16;
        const int MaxInfluencePoints = 64;
        const float MinimumSampleMovementSquared = 0.0001f;

        struct TrailPoint
        {
            public Vector3 position;
            public Vector3 direction;
            public float motionMultiplier;
            public float createdTime;
        }

        static readonly int InteractorCountId = Shader.PropertyToID("_GrassSamplePointCount");
        static readonly int InteractorsId = Shader.PropertyToID("_GrassSamplePointPositions");
        static readonly int InteractorDirectionsId = Shader.PropertyToID("_GrassSamplePointDirections");
        static readonly int InteractorSettingsId = Shader.PropertyToID("_GrassSamplePointSettings");
        static readonly List<StylizedGrassInteractor> ActiveInteractors = new List<StylizedGrassInteractor>(MaxInteractors);
        static readonly Vector4[] Interactors = new Vector4[MaxInfluencePoints];
        static readonly Vector4[] InteractorDirections = new Vector4[MaxInfluencePoints];
        static readonly Vector4[] InteractorSettings = new Vector4[MaxInfluencePoints];

        [Header("即时草地交互")]
        [Tooltip("交互中心相对当前物体的局部坐标偏移。角色根节点不在脚底时，可用它把中心移动到脚边。")]
        public Vector3 centerOffset;

        [Tooltip("交互影响的世界空间半径。半径内的草会从中心向外弯曲，边缘会平滑衰减。")]
        [Range(0.05f, 8f)] public float radius = 1.25f;

        [Tooltip("草片横向弯曲的最大距离，单位为世界空间米。增大后推草效果更明显。")]
        [Range(0f, 3f)] public float bendStrength = 0.85f;

        [Tooltip("草片被压低的程度。0 只弯曲不压低，1 会把中心附近的草压到约四分之一高度。")]
        [Range(0f, 1f)] public float flattenAmount = 0.65f;

        [Tooltip("弯曲方向对物体移动方向的依赖。0 表示始终从交互中心径向推开，1 表示优先顺着物体移动方向倒伏。")]
        [Range(0f, 1f)] public float directionInfluence = 0.65f;

        [Tooltip("从交互中心到范围边缘的衰减曲线。小于 1 时范围较柔和，大于 1 时效果更集中在中心。")]
        [Range(0.25f, 8f)] public float falloff = 1.5f;

        [Tooltip("物体速度对弯曲强度的影响。0 表示静止时也保持完整效果，1 表示静止时不推草、达到设定速度后才获得完整效果。")]
        [Range(0f, 1f)] public float velocityInfluence = 0.2f;

        [Tooltip("物体达到完整速度交互效果所需的世界空间速度。仅在速度影响大于 0 时生效。")]
        [Min(0.01f)] public float speedForFullEffect = 2.5f;

        [Header("持续痕迹与恢复")]
        [Tooltip("物体离开后，旧位置的草恢复到正常状态所需的时间。0 表示关闭持续痕迹并立即恢复。")]
        [Range(0f, 10f)] public float recoveryTime = 2.4f;

        [Tooltip("按固定时间间隔记录一个独立影响点。数值越小采样越密集；每个点只影响自身半径内的草，不会连接成路径。")]
        [Range(0.01f, 0.5f)] public float trailSampleInterval = 0.05f;

        [Tooltip("当前物体最多保留的历史痕迹点数量。增加后可留下更长的路径，但会增加 Shader 交互计算量。")]
        [Range(0, 48)] public int maxTrailPoints = 40;

        readonly List<TrailPoint> trailPoints = new List<TrailPoint>(48);
        Vector3 previousCenter;
        Vector3 previousTrailFrameCenter;
        Vector3 lastRecordedTrailPosition;
        Vector3 velocity;
        float previousTrailFrameTime;
        float nextTrailSampleTime;
        bool hasPreviousCenter;
        bool hasTrailClock;
        bool hasRecordedTrailPosition;

        Vector3 WorldCenter => transform.TransformPoint(centerOffset);
        public int TrailPointCount => trailPoints.Count;

        void OnEnable()
        {
            if (!ActiveInteractors.Contains(this))
                ActiveInteractors.Add(this);

            previousCenter = WorldCenter;
            velocity = Vector3.zero;
            hasPreviousCenter = true;
            ResetTrailSampling(previousCenter);
            UploadGlobalData();
        }

        void OnDisable()
        {
            ActiveInteractors.Remove(this);
            trailPoints.Clear();
            hasTrailClock = false;
            hasRecordedTrailPosition = false;
            UploadGlobalData();
        }

        void OnValidate()
        {
            radius = Mathf.Max(0.05f, radius);
            bendStrength = Mathf.Max(0f, bendStrength);
            falloff = Mathf.Max(0.25f, falloff);
            speedForFullEffect = Mathf.Max(0.01f, speedForFullEffect);
            recoveryTime = Mathf.Max(0f, recoveryTime);
            trailSampleInterval = Mathf.Max(0.01f, trailSampleInterval);
            maxTrailPoints = Mathf.Clamp(maxTrailPoints, 0, 48);
            TrimTrailPoints();

            if (isActiveAndEnabled)
                UploadGlobalData();
        }

        void Update()
        {
            if (!StylizedGrassField.AnyInteractionEnabled)
            {
                previousCenter = WorldCenter;
                velocity = Vector3.zero;
                hasPreviousCenter = true;
                trailPoints.Clear();
                hasTrailClock = false;
                hasRecordedTrailPosition = false;
                return;
            }

            Vector3 center = WorldCenter;
            float deltaTime = Time.deltaTime;
            if (hasPreviousCenter && deltaTime > 0.00001f)
                velocity = (center - previousCenter) / deltaTime;
            else
                velocity = Vector3.zero;

            UpdateTrail(center);
            previousCenter = center;
            hasPreviousCenter = true;
        }

        void LateUpdate()
        {
            RemoveMissingInteractors();
            if (ActiveInteractors.Count > 0 && ActiveInteractors[0] == this)
                UploadGlobalData();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.25f, 0.8f);
            Gizmos.DrawWireSphere(WorldCenter, radius);
        }

        static void RemoveMissingInteractors()
        {
            for (int index = ActiveInteractors.Count - 1; index >= 0; index--)
            {
                if (ActiveInteractors[index] == null || !ActiveInteractors[index].isActiveAndEnabled)
                    ActiveInteractors.RemoveAt(index);
            }
        }

        void UpdateTrail(Vector3 center)
        {
            if (!Application.isPlaying || recoveryTime <= 0f || maxTrailPoints <= 0)
            {
                trailPoints.Clear();
                hasTrailClock = false;
                hasRecordedTrailPosition = false;
                return;
            }

            float currentTime = Time.time;
            RemoveExpiredTrailPoints(currentTime);
            if (!hasTrailClock || currentTime < previousTrailFrameTime)
            {
                previousTrailFrameCenter = center;
                lastRecordedTrailPosition = center;
                previousTrailFrameTime = currentTime;
                nextTrailSampleTime = currentTime + trailSampleInterval;
                hasTrailClock = true;
                hasRecordedTrailPosition = true;
                return;
            }

            float frameDuration = currentTime - previousTrailFrameTime;
            Vector3 frameMovement = center - previousTrailFrameCenter;
            Vector3 direction = frameMovement.sqrMagnitude > 0.000001f
                ? frameMovement.normalized
                : (velocity.sqrMagnitude > 0.000001f ? velocity.normalized : Vector3.zero);
            float motionMultiplier = CalculateMotionMultiplier();
            int generatedSamples = 0;
            while (nextTrailSampleTime <= currentTime && generatedSamples < MaxInfluencePoints)
            {
                float frameT = frameDuration > 0.00001f
                    ? Mathf.Clamp01((nextTrailSampleTime - previousTrailFrameTime) / frameDuration)
                    : 1f;
                Vector3 samplePosition = Vector3.Lerp(previousTrailFrameCenter, center, frameT);
                if (!hasRecordedTrailPosition
                    || (samplePosition - lastRecordedTrailPosition).sqrMagnitude >= MinimumSampleMovementSquared)
                {
                    AddTrailPoint(new TrailPoint
                    {
                        position = samplePosition,
                        direction = direction,
                        motionMultiplier = motionMultiplier,
                        createdTime = nextTrailSampleTime
                    });
                    lastRecordedTrailPosition = samplePosition;
                    hasRecordedTrailPosition = true;
                }

                nextTrailSampleTime += trailSampleInterval;
                generatedSamples++;
            }

            if (generatedSamples >= MaxInfluencePoints && nextTrailSampleTime <= currentTime)
                nextTrailSampleTime = currentTime + trailSampleInterval;

            previousTrailFrameCenter = center;
            previousTrailFrameTime = currentTime;
        }

        void ResetTrailSampling(Vector3 center)
        {
            trailPoints.Clear();
            previousTrailFrameCenter = center;
            lastRecordedTrailPosition = center;
            previousTrailFrameTime = Time.time;
            nextTrailSampleTime = previousTrailFrameTime + trailSampleInterval;
            hasTrailClock = Application.isPlaying;
            hasRecordedTrailPosition = true;
        }

        void AddTrailPoint(TrailPoint point)
        {
            while (trailPoints.Count >= maxTrailPoints && trailPoints.Count > 0)
                trailPoints.RemoveAt(0);

            trailPoints.Add(point);
        }

        void TrimTrailPoints()
        {
            if (recoveryTime <= 0f || maxTrailPoints <= 0)
            {
                trailPoints.Clear();
                return;
            }

            while (trailPoints.Count > maxTrailPoints)
                trailPoints.RemoveAt(0);
        }

        void RemoveExpiredTrailPoints(float currentTime)
        {
            if (recoveryTime <= 0f)
            {
                trailPoints.Clear();
                return;
            }

            while (trailPoints.Count > 0
                && currentTime - trailPoints[0].createdTime >= recoveryTime)
            {
                trailPoints.RemoveAt(0);
            }
        }

        float CalculateMotionMultiplier()
        {
            float speedFactor = Mathf.Clamp01(velocity.magnitude / speedForFullEffect);
            return Mathf.Lerp(1f, speedFactor, velocityInfluence);
        }

        static void UploadGlobalData()
        {
            if (!StylizedGrassField.AnyInteractionEnabled)
            {
                Shader.SetGlobalInt(InteractorCountId, 0);
                return;
            }

            RemoveMissingInteractors();
            int activeCount = Mathf.Min(ActiveInteractors.Count, MaxInteractors);
            int count = 0;

            for (int index = 0; index < activeCount; index++)
            {
                StylizedGrassInteractor interactor = ActiveInteractors[index];
                Vector3 direction = interactor.velocity.sqrMagnitude > 0.000001f
                    ? interactor.velocity.normalized
                    : Vector3.zero;
                WriteInfluencePoint(
                    count++,
                    interactor,
                    interactor.WorldCenter,
                    direction,
                    interactor.CalculateMotionMultiplier(),
                    1f);
            }

            float currentTime = Time.time;
            for (int interactorIndex = 0;
                interactorIndex < activeCount && count < MaxInfluencePoints;
                interactorIndex++)
            {
                StylizedGrassInteractor interactor = ActiveInteractors[interactorIndex];
                interactor.RemoveExpiredTrailPoints(currentTime);

                for (int trailIndex = interactor.trailPoints.Count - 1;
                    trailIndex >= 0 && count < MaxInfluencePoints;
                    trailIndex--)
                {
                    TrailPoint trailPoint = interactor.trailPoints[trailIndex];
                    float recoveryFade = CalculateRecoveryFade(
                        currentTime,
                        trailPoint.createdTime,
                        interactor.recoveryTime);
                    WriteInfluencePoint(
                        count++,
                        interactor,
                        trailPoint.position,
                        trailPoint.direction,
                        trailPoint.motionMultiplier,
                        recoveryFade);
                }
            }

            Shader.SetGlobalInt(InteractorCountId, count);
            if (count == 0)
                return;

            Shader.SetGlobalVectorArray(InteractorsId, Interactors);
            Shader.SetGlobalVectorArray(InteractorDirectionsId, InteractorDirections);
            Shader.SetGlobalVectorArray(InteractorSettingsId, InteractorSettings);
        }

        static float CalculateRecoveryFade(float currentTime, float createdTime, float duration)
        {
            float t = Mathf.Clamp01((currentTime - createdTime) / Mathf.Max(duration, 0.0001f));
            float smootherStep = t * t * t * (t * (t * 6f - 15f) + 10f);
            return 1f - smootherStep;
        }

        static void WriteInfluencePoint(
            int index,
            StylizedGrassInteractor interactor,
            Vector3 position,
            Vector3 direction,
            float motionMultiplier,
            float recoveryFade)
        {
            Interactors[index] = new Vector4(
                position.x,
                position.y,
                position.z,
                interactor.radius);
            InteractorDirections[index] = new Vector4(
                direction.x,
                direction.y,
                direction.z,
                interactor.bendStrength * motionMultiplier);
            InteractorSettings[index] = new Vector4(
                interactor.flattenAmount,
                interactor.directionInfluence,
                interactor.falloff,
                recoveryFade);
        }
    }
}
