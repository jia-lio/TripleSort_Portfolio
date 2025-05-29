using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Framework;
using Game.Model;
using Game.View;
using I2.Loc;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Controller
{
    public class BoxController : BasicBox
    {
        public BoxView view;
        public Rigidbody2D rigidbody;
        
        public Subject<BoxController> OnBoxClearSubject { get; } = new();
        public Subject<bool> isThingMoving { get; } = new();

        public int boxOpenCount { get; private set; }
        
        public override BasicBoxModel Model => _model;
        private BoxModel _model;

        private List<bool> hiddenList = new();
        private int _hiddenCount;
        
        
        public void Init(BoxModel model, List<(EThingType, EThingColorType)> typeList, bool isBoxSoldOut, int hiddenCount)
        {
            _model = model;
            _model.SetIsSoldOut(isBoxSoldOut);
            _hiddenCount = hiddenCount;
            rigidbody.bodyType = _model.isGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Static;
            
            hiddenList = HiddenSetting(typeList);
            InitializeThings(typeList);
            
            SetBoxOpenCount(_model.boxOpenCount, false);
            
            UpdateAnimationView();
            NoneCheck(Unit.Default);

            view.SetBoxSprite(Model.boxBody);

            OnThingCheck.Subscribe(ThingCheck).AddTo(this);
            OnNoneCheck.Subscribe(NoneCheck).AddTo(this);
        }

        protected override void InitializeThings(List<(EThingType, EThingColorType)> typeList)
        {
            for (int i = 0; i < typeList.Count; i++)
            {
                var thingModel = new ThingModel(typeList[i].Item1, typeList[i].Item2, hiddenList[i]);
                totalThingModel.Add(thingModel);
            }
        }
        
        protected override async void ThingCheck(ThingController thingController)
        {
            var value = thingControllers.Count(thing => 
                    thing.Model.type == thingController.Model.type && 
                    thing.Model.colorType == thingController.Model.colorType &&
                    thingController.Model.type != EThingType.None &&
                    thingController.Model.colorType != EThingColorType.None);

            if (value < 3)
            {
                thingController.view.PlayAnim().Forget();

                if (thingController.Model.type != EThingType.None)
                {
                    thingController.view.PlayLandingSound();
                }
                
                OnStageFailedCheck.OnNext(Unit.Default);
            }
            else
            {
                foreach (var thing in thingControllers)
                {
                    thing.boxCollider.enabled = false;
                    thing.view.PlayAnim().Forget();
                }
                
                await UniTask.WaitForSeconds(0.15f);
                SoundManager.Instance.PlaySFX(Address.MATCH_MP3);

                foreach (var thing in thingControllers)
                {
                    thing.view.PlayClearParticle();
                }

                await UniTask.WaitForSeconds(0.25f);

                foreach (var thing in thingControllers)
                {
                    thing.boxCollider.enabled = true;
                }
                
                ClearBox();
                
                isThingMoving.OnNext(true);
                OnBoxClearSubject.OnNext(this);
                isThingMoving.OnNext(false);
                OnStageFailedCheck.OnNext(Unit.Default);
            }
        }

        protected override void NoneCheck(Unit arg)
        {
            var value = thingControllers.Count(thing => thing.Model.type == EThingType.None);
            if (value >= 3)
            {
                ClearBox();

                if (totalThingModel.Count > 0)
                {
                    NoneCheck(Unit.Default);
                }
            }
        }

        protected override void ClearBox()
        {
            base.ClearBox();

            UpdateItemList();
            ShowCurrentItem();
            ShowNextItem();
            
            if (IsSoldOutOrGravityCheck(_model._isSoldOut))
            {
                SetIsSoldOut(true);
                view.SoldOutAnimation();
            }
            else if (IsSoldOutOrGravityCheck(_model.isGravity))
            {
                gameObject.SetActive(false);
            }
            
            ThingUpdateView();
        }

        protected override void UpdateItemList()
        {
            base.UpdateItemList();
            
            hiddenList.RemoveRange(0, Mathf.Min(3, hiddenList.Count));
        }

        protected override void ShowCurrentItem()
        {
            for (var i = 0; i < thingControllers.Count; i++)
            {
                if (i < totalThingModel.Count && currentIndex + i < totalThingModel.Count)
                {
                    var thingModel = totalThingModel[i];
                    thingModel.SetPosition(thingControllers[i].transform.position);
                    thingModel.SetLayer(2);
                    thingControllers[i].Init(thingModel, this);
                }
                else
                {
                    thingControllers[i].Init(nextThingControllers[i].Model, this);
                    thingControllers[i].SetLayer(2);
                }
            }
        }

        protected override void ShowNextItem()
        {
            if (NextTypeCheck() == false)
            {
                var nextBoxCount = nextThingControllers.Count;
                for (int i = 0; i < nextBoxCount; i++)
                {
                    if (i < totalThingModel.Count && currentIndex + nextBoxCount + i < totalThingModel.Count)
                    {
                        var thingModel = totalThingModel[currentIndex + 3 + i];
                        thingModel.SetPosition(nextThingControllers[i].transform.position);
                        thingModel.SetLayer(1);
                        nextThingControllers[i].Init(thingModel, this);
                    }
                    else
                    {
                        var thingModel = new ThingModel(EThingType.None, EThingColorType.None, false);
                        thingModel.SetPosition(nextThingControllers[i].transform.position);
                        thingModel.SetLayer(1);
                        nextThingControllers[i].Init(thingModel, this);
                    }
                }
            }
            
            OnStageFailedCheck.OnNext(Unit.Default);
        }

        public override void Return()
        {
            Destroy(view.gameObject);

            base.Return();
        }

        private bool IsSoldOutOrGravityCheck(bool isCheck)
        {
            var value = thingControllers.Count(thingController => thingController.Model.type == EThingType.None);

            if (value >= 3 && isCheck)
                return true;

            return false;
        }
        
        private void SettingThingCollider()
        {
            foreach (var thing in thingControllers)
            {
                thing.SetThingCollider(boxOpenCount <= 0);
            }
        }

        private async UniTask BoxOpenCheck(bool isSound)
        {
            SettingThingCollider();

            if (_model.boxOpenCount > 0)
            {
                view.SetBoxLockActive(_model.boxOpenCount > 0);
                view.SetBoxLockCount(boxOpenCount);
                
                view.LockUpdate(boxOpenCount, isSound).Forget();
            }
        }

        public void SetBoxOpenCount(int value, bool isSound)
        {
            boxOpenCount = value;
            BoxOpenCheck(isSound).Forget();
        }

        protected override void ThingUpdateView()
        {
            base.ThingUpdateView();

            foreach (var nextThingController in nextThingControllers)
            {
                nextThingController.UpdateAnimationView();
            }
        }

        private List<bool> HiddenSetting(List<(EThingType, EThingColorType)> typeList)
        {
            int hiddenCount = _hiddenCount;
            int count = typeList.Count;
            var isHidden = new List<bool>(new bool[count]);

            var validIndices = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (typeList[i].Item1 != EThingType.None)
                {
                    validIndices.Add(i);
                }
            }

            int validCount = validIndices.Count;
            if (validCount == 0) 
                return isHidden;

            while (hiddenCount > 0)
            {
                int randomIndex = validIndices[Random.Range(0, validCount)];
                if (isHidden[randomIndex] == false)
                {
                    isHidden[randomIndex] = true;
                    hiddenCount--;
                }
            }

            return isHidden;
        }
        
        public override (bool, int) HammerItemRemoveType((EThingType, EThingColorType) removeTypes, int removeCount)
        {
            var isRemove = base.HammerItemRemoveType(removeTypes, removeCount);

            if (isRemove.Item1)
            {
                NoneCheck(Unit.Default);
                UpdateView();
            }
            
            return isRemove;
        }

        public override void MagicWandItemChangeThing((EThingType, EThingColorType) changeThing, HashSet<(EThingType, EThingColorType)> changeThings)
        {
            base.MagicWandItemChangeThing(changeThing, changeThings);
            
            NoneCheck(Unit.Default);
            ThingUpdateView();
        }

        public override void ChangeTotalThingUpdateView()
        {
            //UpdateAnimationView();
            ThingUpdateView();
        }

        protected override void UpdateAnimationView()
        {
            ShowCurrentItem();
            ShowNextItem();
            ThingUpdateView();
        }

        protected override void UpdateView()
        {
            ShowCurrentItem();
            ShowNextItem();

            foreach (var thing in thingControllers)
            {
                thing.UpdateView();
            }

            foreach (var nextThing in nextThingControllers)
            {
                nextThing.UpdateView();
            }
        }
        
        public void NoneTypeSetRandomPosition()
        {
            for (int i = 0; i < thingControllers.Count; i++)
            {
                if (thingControllers[i].Model.type == EThingType.None && boxOpenCount > 0)
                {
                    var randomIndex = Random.Range(0, Mathf.Min(3, thingControllers.Count));
                    if(randomIndex == i) continue;
                    
                    var tempModel = new ThingModel(thingControllers[i].Model.type, thingControllers[i].Model.colorType, thingControllers[i].Model.isHidden);
                    thingControllers[i].ChangeModel(thingControllers[randomIndex].Model, thingControllers[i].GetStartPosition());
                    thingControllers[randomIndex].ChangeModel(tempModel, thingControllers[randomIndex].GetStartPosition());
                }
            }
        }

        public override void SwapThingModel()
        {
            for (int i = 0; i < thingControllers.Count; i++)
            {
                if (i < totalThingModel.Count)
                {
                    totalThingModel[i] = thingControllers[i].Model;
                }
                else
                {
                    totalThingModel.Add(thingControllers[i].Model);
                }
            }
        }

        public void LockBoxAd()
        {
            AdsManager.ShowRewardVideo(() =>
            {
                SetBoxOpenCount(0, true);
            }, "Ingame_BoxLock");
        }

        private bool NextTypeCheck()
        {
            var nextBoxCount = nextThingControllers.Count;
            var noneCount = 0;

            while (true)
            {
                for (int i = 0; i < nextBoxCount; i++)
                {
                    if (i < totalThingModel.Count && currentIndex + nextBoxCount + i < totalThingModel.Count)
                    {
                        var thingModel = totalThingModel[currentIndex + nextBoxCount + i];
                        if (thingModel.type == EThingType.None)
                        {
                            noneCount++;
                        }
                    }
                }

                if (noneCount >= 3)
                {
                    totalThingModel.RemoveRange(currentIndex + nextBoxCount, Mathf.Min(3, totalThingModel.Count));

                    noneCount = 0;
                    continue;
                }
                
                break;
            }

            return false;
        }
        
        [Button]
        public void TestDebug()
        {
            DebugLogger.Log("------------------------------------------------------");
            for (int i = 0; i < thingControllers.Count; i++)
            {
                DebugLogger.Log($"thing : {thingControllers[i].Model.type} / {thingControllers[i].Model.colorType}");
                DebugLogger.Log($"total : {totalThingModel[i].type} / {totalThingModel[i].colorType}");
            }
            DebugLogger.Log("------------------------------------------------------");
        }
    }
}