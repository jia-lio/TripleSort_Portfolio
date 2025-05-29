using Cysharp.Threading.Tasks;
using Game.Controller;
using Game.Model;
using R3;
using UnityEngine;

namespace Game.Pool
{
    public class BoxFactory
    {
        private readonly PoolContainer _pool;

        public BoxFactory(PoolContainer pool)
        {
            _pool = pool;
        }

        public async UniTask<BoxController> GetAsync(BoxModel model, Transform parent)
        {
            var controller = await _pool.Box.Rent(parent);
            controller.OnReturn
                .Subscribe(_ => _pool.Box.Return(controller))
                .AddTo(controller.Disposable);

            return controller;
        }
        
        public async UniTask<GimmickBoxController> GetGimmickAsync(GimmickBoxModel model, Transform parent)
        {
            var controller = await _pool.GimmickBox.Rent(parent);
            controller.OnReturn
                .Subscribe(_ => _pool.GimmickBox.Return(controller))
                .AddTo(controller.Disposable);

            return controller;
        }
        
        public async UniTask<RailController> GetRailAsync(RailModel model, Transform parent)
        {
            var controller = await _pool.Rail.Rent(parent);
            controller.OnReturn
                .Subscribe(_ => _pool.Rail.Return(controller))
                .AddTo(controller.Disposable);

            return controller;
        }
        
        public async UniTask<ShelfController> GetShelfAsync(ShelfModel model, Transform parent)
        {
            var controller = await _pool.Shelf.Rent(parent);
            controller.OnReturn
                .Subscribe(_ => _pool.Shelf.Return(controller))
                .AddTo(controller.Disposable);

            return controller;
        }
    }
}