using System.Threading.Tasks;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Every gameplay scene has a SceneRoot. Responsible for constructing gameplay-scoped objects
    /// (BoardController, TileController, GameFlowController) and their dependencies.
    /// Lives forever in the scene and hands them to various managers.
    /// </summary>
    public abstract class SceneRoot : MonoBehaviour
    {
        protected virtual async Task Initialize()
        {
            await Task.CompletedTask;
        }

        protected virtual void Awake()
        {
            _ = Initialize();
        }
    }
}
