using System;
using System.Collections.Generic;

public class Observable<T> : IObservable<T>
{
    public IReadOnlyList<IObserver<T>> Observers => _observers;

    private readonly List<IObserver<T>> _observers = new();

    /// <summary> Subscribe to a data source </summary>
    public virtual IDisposable Subscribe(IObserver<T> observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }

    private class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }

        public void Dispose()
        {
            if (_observer != null && _observers.Contains(_observer))
                _observers.Remove(_observer);
        }
    }
}