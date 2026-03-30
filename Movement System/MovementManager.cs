using Movement.Controllers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MovementManager : MonoBehaviour
{
    public static MovementManager Instance { get; private set; }

    private Dictionary<string, AbstractMovementController> _controllers = new();


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject manager = new(nameof(MovementManager));
        Instance = manager.AddComponent<MovementManager>();
        DontDestroyOnLoad(manager);
    }

    private void Update()
    {
        foreach (AbstractMovementController controller in _controllers.Values)
            controller.UpdateMovers();
    }

    /// <summary> Set MovementType for MoverGroup Сontrollers </summary> 
    public void SetMovementType(MoverGroup group)
    {
        List<MovementMover> movers = FindObjectsByType<MovementMover>(FindObjectsSortMode.None).Where(mover => mover.enabled).ToList();

        foreach (MovementMover mover in movers)
            if (mover.ID.Contains(group.ToString()))
                mover.Unsubscribe();

        foreach (MovementMover mover in movers)
            if (mover.ID.Contains(group.ToString()))
                mover.Subscribe();
    }

    /// <summary> Get Controller target MovementMover </summary>
    public AbstractMovementController GetController(MovementMover mover)
    {
        //Debug.Log($"<color=orange>Запрошен контроллер c ID - {mover.ID}</color>");
        if (_controllers.ContainsKey(mover.ID))
            return _controllers[mover.ID];
        else
            return AddController(mover);
    }

    private AbstractMovementController AddController(MovementMover mover)
    {
        AbstractMovementController controller = GetMovementType(mover) switch
        {
            (MovementType.GeoReciever) => new GeoRecieverController(mover),
            (MovementType.GeoDebug) => new GeoDebugController(mover),
            (MovementType.Trajectory) => new TrajectoryController(mover),
            (MovementType.FreeCamera) => new FreeCameraController(mover),
            _ => new NoneController(mover)
        };
        _controllers.Add(mover.ID, controller);

        //Debug.Log($"<color=green>Контроллер - {controller.GetType()} ({controller.ID}) создан</color>");
        return controller;
    }

    /// <summary> Dispose and destroy target Controller </summary>
    public void DestroyController(AbstractMovementController controller)
    {
        controller.Dispose();
        _controllers.Remove(controller.ID);
        //Debug.Log($"<color=red>Контроллер - {controller.GetType()} ({controller.ID}) уничтожен</color>");
    }

    private MovementType GetMovementType(MovementMover mover)
    {
        if (mover.ID.Contains(MoverGroup.Player.ToString()))
            return SaveManager.Instance.Settings.Movement.MovementType;

        if (mover.ID.Contains(MoverGroup.Agent.ToString()))
            return MovementType.Trajectory;

        return MovementType.None;
    }


#if UNITY_EDITOR

    [Header("- - Debug:")]
    [SerializeField] private MoverGroup _moverGroup;
    [SerializeField] private MovementType _movementType;

    [InspectorButton("Set Movement Type")]
    public void SetMovementTypeDebug()
    {
        if (_moverGroup == MoverGroup.Player)
            SaveManager.Instance.Settings.Movement.MovementType = _movementType;

        SetMovementType(_moverGroup);
    }

    [InspectorButton("How many controllers exist?")]
    public void LogControllersCount() => Debug.Log($"<color=cyan>Текущее количество контроллеров - {_controllers.Keys.Count}</color>");

#endif
}