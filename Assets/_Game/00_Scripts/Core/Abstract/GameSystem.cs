using Slafurry.System.Bootstrap;

namespace Slafurry.Core.Abstract
{
    public abstract class GameSystem<T> : Singleton<T>, IGameSystemLifecycle where T : GameSystem<T>
    {
        protected override void OnSingletonAwake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}