namespace Movement.Controllers
{
    /// <summary> The empty controller of none movement in WorldSpace </summary> 
    public class NoneController : AbstractMovementController
    {
        public NoneController(MovementMover mover)
        {
            ID = mover.ID;
        }

        public override void UpdateMovers() { }
    }
}