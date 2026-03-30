using System;
using UnityEngine;

namespace Multilayer.Setup.Module
{
    public abstract class BaseSetupModule : MonoBehaviour, IDisposable
    {
        /// <summary> Check is module valid </summary>
        protected abstract bool IsModuleValid();

        /// <summary> Dispose module resources </summary>
        public abstract void Dispose();
    }
}