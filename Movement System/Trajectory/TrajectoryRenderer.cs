using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Movement.Trajectories
{
    [ExecuteInEditMode, RequireComponent(typeof(Trajectory))]
    public class TrajectoryRenderer : MonoBehaviour
    {
#if UNITY_EDITOR
        private enum VisualizeType { Player, Bot, Selection }

        [Header("- - Visualization:")]
        [SerializeField] private VisualizeType _type;
        [SerializeField, Range(0.1f, 5f)] private float _lineRendererWidth = 1;
        [SerializeField] private bool _showTrajectoryPath = true;
        [SerializeField] private bool _showTurns = true;
        [SerializeField] private bool _showBounds = false;

        [Header("- - Info:")]
        [SerializeField, DisableEdit] private float _trajectoryLength;

        private Trajectory _trajectory;
        private LineRenderer _lineRenderer;
        private Material _lineRendererMaterial;

        private float _colorVariation;
        private GUIStyle _labelStyle;
        private Color _baseColor;
        private Color _additionalColor;


        private void Awake()
        {
            _trajectory = GetComponent<Trajectory>();
            _lineRenderer = GetComponent<LineRenderer>();
            InitializeLineRenderer();

            _colorVariation = Random.Range(-0.07f, 0.07f);
            _baseColor = GetBaseColorByVisualizeType();
            _additionalColor = new Color(1 - _baseColor.r, 1 - _baseColor.g, 1 - _baseColor.b);
        }

        private void InitializeLineRenderer()
        {
            if (!_lineRenderer)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            else
                _lineRenderer.enabled = true;

            _lineRenderer.numCornerVertices = 3;
            _lineRenderer.numCapVertices = 3;
            _lineRenderer.textureMode = LineTextureMode.Tile;
            _lineRenderer.alignment = LineAlignment.TransformZ;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.position = new Vector3(0, 0, 0);
            transform.eulerAngles = new Vector3(90, 0, 0);

            Shader trajectoryShader = Shader.Find("Shader Graphs/Trajectory");
            if (trajectoryShader != null)
            {
                _lineRendererMaterial = new Material(trajectoryShader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = $"{gameObject.name}_M"
                };
                _lineRenderer.sharedMaterial = _lineRendererMaterial;
            }
        }

        private void OnEnable() { _lineRenderer.enabled = true; }

        private void OnValidate()
        {
            _baseColor = GetBaseColorByVisualizeType();
            _additionalColor = new Color(1 - _baseColor.r, 1 - _baseColor.g, 1 - _baseColor.b);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_trajectory || !_trajectory.IsValid()) return;

            UpdateLineRenderer();

            if (_showTrajectoryPath) DrawTrajectoryPath();
            if (_showTurns) DrawTurnsCurves();
            if (_showBounds) DrawTurnsBounds();
        }

        private void UpdateLineRenderer()
        {
            if (!_lineRenderer) return;

            _lineRenderer.widthMultiplier = _lineRendererWidth;
            if (_lineRendererMaterial != null)
                _lineRendererMaterial.color = _baseColor;

            List<Vector3> path = UtilityTrajectory.GetTrajectoryPath(_trajectory);
            _lineRenderer.positionCount = path.Count;
            _lineRenderer.SetPositions(path.ToArray());
        }

        private void DrawTrajectoryPath()
        {
            Gizmos.color = _baseColor;
            for (int i = 1; i < _trajectory.Turns.Count; i++)
                Gizmos.DrawLine(_trajectory.Turns[i - 1].Apex, _trajectory.Turns[i].Apex);
            for (int i = 0; i < _trajectory.Turns.Count; i++)
                Gizmos.DrawWireSphere(_trajectory.Turns[i].Apex, 0.25f);
        }

        private void DrawTurnsCurves()
        {
            Gizmos.color = _additionalColor;
            for (int i = 0; i < _trajectory.Turns.Count; i++)
            {
                var segments = UtilityTrajectory.GenerateTurnPath(_trajectory.Turns[i]);
                for (int s = 1; s < segments.Count; s++)
                    Gizmos.DrawLine(segments[s - 1], segments[s]);
            }
        }

        private void DrawTurnsBounds()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle();
                _labelStyle.normal.textColor = Color.white;
                _labelStyle.fontStyle = FontStyle.Bold;
                _labelStyle.fontSize = 16;
                _labelStyle.alignment = TextAnchor.MiddleCenter;
            }

            Gizmos.color = _additionalColor;
            for (int i = 0; i < _trajectory.Turns.Count; i++)
            {
                Gizmos.DrawWireSphere(_trajectory.Turns[i].Start, 0.25f);
                Gizmos.DrawWireSphere(_trajectory.Turns[i].End, 0.25f);
                Handles.Label(_trajectory.Turns[i].Apex + Vector3.up * 2, "Turn №" + i, _labelStyle);
            }
        }

        private Color GetBaseColorByVisualizeType()
        {
            float hue = 0;
            switch (_type)
            {
                case VisualizeType.Player:
                    hue = 0.6f;
                    break;
                case VisualizeType.Bot:
                    hue = 0.1f;
                    break;
                case VisualizeType.Selection:
                    hue = 0.95f;
                    break;
            }
            hue = Mathf.Repeat(hue + _colorVariation, 1f);
            return Color.HSVToRGB(hue, 0.8f, 1);
        }

        private void OnDisable() { _lineRenderer.enabled = false; }

#endif
    }
}