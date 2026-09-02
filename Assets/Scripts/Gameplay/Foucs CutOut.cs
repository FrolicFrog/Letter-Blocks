using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class FocusCutOut : MonoBehaviour
{
    [Serializable]
    public class CutoutGroup
    {
        [Tooltip("Custom label for clarity in Inspector")]
        public string groupName = "Cutout";
        [Tooltip("Renderers belonging to this specific hole")]
        public List<Renderer> renderers = new List<Renderer>();
        [Header("Group Offsets")]
        [Range(-0.5f, 0.5f)] public float offsetX = 0.0f;
        [Range(-0.5f, 0.5f)] public float offsetY = 0f;
        [Range(0.5f, 2.5f)] public float sizeMultiplier = 1.0f;
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Cutout Groups")]
    [SerializeField] public List<CutoutGroup> cutoutGroups = new List<CutoutGroup>();

    [Header("Global Settings")]
    [SerializeField] private Vector2 padding = new Vector2(0.015f, 0.015f);
    public RectTransform textBox;
    private Material _matInstance;
    private Image _image;

    private static readonly int CutoutCountProp = Shader.PropertyToID("_CutoutCount");
    private static readonly int CentersProp = Shader.PropertyToID("_Centers");
    private static readonly int HalfSizesProp = Shader.PropertyToID("_HalfSizes");
    private static readonly int AspectProp = Shader.PropertyToID("_Aspect");

    public static FocusCutOut instance;

    // INCREASED LIMIT TO 64
    private const int MAX_CUTOUTS = 64;
    private readonly Vector4[] _centersArray = new Vector4[MAX_CUTOUTS];
    private readonly Vector4[] _halfSizesArray = new Vector4[MAX_CUTOUTS];

    void Awake()
    {
        instance = this;
        InitMaterial();
    }

    void OnValidate()
    {
        InitMaterial();
    }

    private void InitMaterial()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (_image == null) _image = GetComponent<Image>();

        if (_image != null && (_matInstance == null || _matInstance != _image.material))
        {
            if (Application.isPlaying)
            {
                _matInstance = Instantiate(_image.material);
                _image.material = _matInstance;
            }
            else
            {
                _matInstance = _image.material;
            }
        }
    }

    void LateUpdate()
    {
        if (cutoutGroups == null || cutoutGroups.Count == 0 || targetCamera == null || _matInstance == null)
            return;

        UpdateMultiCutouts();
    }

    private void UpdateMultiCutouts()
    {
        int validGroupCount = 0;

        // UPDATED LOOP CONDITION TO CHECK AGAINST MAX_CUTOUTS
        for (int g = 0; g < cutoutGroups.Count && validGroupCount < MAX_CUTOUTS; g++)
        {
            CutoutGroup group = cutoutGroups[g];
            if (group == null || group.renderers == null || group.renderers.Count == 0) continue;

            Vector2 minViewport = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxViewport = new Vector2(float.MinValue, float.MinValue);
            bool hasValidRenderer = false;

            for (int r = 0; r < group.renderers.Count; r++)
            {
                Renderer rend = group.renderers[r];
                if (rend == null || !rend.gameObject.activeInHierarchy) continue;

                hasValidRenderer = true;
                Bounds bounds = rend.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                Vector3[] corners = new Vector3[8]
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z)
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 vp = targetCamera.WorldToViewportPoint(corners[i]);
                    minViewport = Vector2.Min(minViewport, vp);
                    maxViewport = Vector2.Max(maxViewport, vp);
                }
            }

            if (!hasValidRenderer) continue;

            Vector2 center = (minViewport + maxViewport) * 0.5f;
            center.x += group.offsetX;
            center.y += group.offsetY;

            float mult = group.sizeMultiplier <= 0 ? 1f : group.sizeMultiplier;
            Vector2 halfSize = (((maxViewport - minViewport) * 0.5f) + padding) * mult;

            _centersArray[validGroupCount] = new Vector4(center.x, center.y, 0, 0);
            _halfSizesArray[validGroupCount] = new Vector4(halfSize.x, halfSize.y, 0, 0);
            validGroupCount++;
        }

        float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);

        _matInstance.SetInt(CutoutCountProp, validGroupCount);
        _matInstance.SetVectorArray(CentersProp, _centersArray);
        _matInstance.SetVectorArray(HalfSizesProp, _halfSizesArray);
        _matInstance.SetFloat(AspectProp, aspect);
    }
}