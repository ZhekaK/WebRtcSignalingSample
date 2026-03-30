using Movement.Controllers;
using Movement.Trajectories;
using UnityEngine;

public enum MoverGroup { Player, Agent }

public class MovementMover : MonoBehaviour
{
    [Header("- - Settings:")]
    [SerializeField] private MoverGroup _initGroup = MoverGroup.Player;
    [SerializeField, Min(0)] private int _initNumber = 0;
    [Space]
    [SerializeField] private Transform _offsetTransform;
    [SerializeField] private Trajectory _trajectory;

    private AbstractMovementController _controller;
    private MoverGroup _group;
    private int _number;

    public string ID => $"{_group}_{_number}";
    public Trajectory Trajectory => _trajectory;


    private void Awake()
    {
        _group = _initGroup;
        _number = _initNumber;
    }

    private void OnEnable() { Subscribe(); }

    /// <summary> Subscribe MovementMover to relevant Controller </summary>
    public void Subscribe()
    {
        _controller = MovementManager.Instance.GetController(this);
        _controller.SubscribeMover(this);
        //Debug.Log($"<color=yellow>Перемещатель - {name} ({ID}) подписался на контроллер - {_controller.GetType()} ({_controller.ID})</color>");
    }

    /// <summary> Unsubscribe MovementMover from related Controller </summary>
    public void Unsubscribe()
    {
        if (_controller == null) return;

        _controller.UnsubscribeMover(this);
        if (_controller.IsEmpty) MovementManager.Instance.DestroyController(_controller);
        //Debug.Log($"<color=yellow>Перемещатель - {name} ({ID}) отписался от контроллера - {_controller.GetType()} ({_controller.ID})</color>");
    }

    /// <summary> Resubscribe Mover to controller </summary>
    public void Resubscribe(MoverGroup group, int index)
    {
        //Debug.Log($"<color=cyan>Перемещатель - {name} из ({ID}) стал ({_initGroup}_{_initNumber}) </color>");
        Unsubscribe();
        _group = group;
        _number = index;
        Subscribe();
    }

    /// <summary> Update MovementMover transform </summary>
    public void UpdateTransform(UnityTransform unityTransform)
    {
        transform.position = unityTransform.position;
        transform.rotation = unityTransform.rotation;

        if (_offsetTransform)
            _offsetTransform.localPosition = unityTransform.offset;
    }

    private void OnDisable() { Unsubscribe(); }

#if UNITY_EDITOR

    [InspectorButton("Resubscribe")]
    private void ResubscribeEditor()
    {
        if (this.enabled && _controller != null)
            Resubscribe(_initGroup, _initNumber);
    }

#endif
}