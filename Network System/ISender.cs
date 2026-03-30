using System.Threading.Tasks;
using UnityEngine;

public interface ISender<T>
{
    Task WaitToSendAsync(T message);
}
