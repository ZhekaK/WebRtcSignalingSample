using UnityEngine;

public abstract class UpdateServiceBase<T> : MonoBehaviour
//where T : struct
{
    public T Data { get; protected set; }

    private bool isDirty = false;

    private void Update()
    {
        if (isDirty)
        {
            isDirty = false;
            UpdateService(Data);
        }
    }

    public void UpdateData(T data)
    {
        Data = data;
        isDirty = true;
    }

    protected abstract void UpdateService(T data);
}
