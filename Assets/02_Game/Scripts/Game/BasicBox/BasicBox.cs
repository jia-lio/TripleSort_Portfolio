using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Model;
using Game.Pool;
using R3;
using Spine;
using UnityEngine;

namespace Game.Controller
{
    public abstract class BasicBox : PoolObject
    {
        public List<ThingController> thingControllers;
        public List<ThingController> nextThingControllers;
        
        public Subject<ThingController> OnThingCheck { get; } = new();
        public Subject<Unit> OnNoneCheck { get; } = new();
        public Subject<Unit> OnStageFailedCheck { get; } = new();
        public Subject<ThingController> OnGoldThingSubject { get; } = new();
        
        public abstract BasicBoxModel Model { get; }

        protected List<ThingModel> totalThingModel { get; set; } = new();
        public List<ThingModel> GetTotalThingModel() => totalThingModel;
        
        protected readonly int currentIndex = 0;
        protected int hammerRemoveCount = 0;
        protected float stopRailSec = 0f;
        
        public bool _isSoldOut { get; private set; }

        protected virtual void InitializeThings(List<(EThingType, EThingColorType)> typeList) { }
        protected virtual void ShowNextItem() { }
        protected virtual void ThingCheck(ThingController thingController) { }
        protected virtual void NoneCheck(Unit arg) { }
        protected virtual void UpdateAnimationView() { }
        protected virtual void UpdateView() { }
        public virtual void SwapThingModel() { }
        public virtual void ChangeTotalThingUpdateView() { }
        protected virtual void ShowCurrentItem() { }
        
        public async UniTask MovingAsync(Vector3 startPos, Vector3 endPos, Vector3 move)
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                while (true)
                {
                    transform.localPosition += move * (Time.deltaTime * 0.8f);
                
                    if ((move.x < 0 && transform.position.x < endPos.x) ||
                        (move.x > 0 && transform.position.x > endPos.x) ||
                        (move.y < 0 && transform.position.y < endPos.y) ||
                        (move.y > 0 && transform.position.y > endPos.y))
                    {
                        transform.position = startPos;
                    }
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        public void StopMoveBox()
        {
            stopRailSec = 2.5f;
        }

        public void RailMove()
        {
            stopRailSec = 0f;
        }

        protected virtual void ClearBox()
        {
            foreach (var thingController in thingControllers)
            {
                thingController.Clear();
            }
        }
        
        protected virtual void UpdateItemList()
        {
            try
            {
                totalThingModel.RemoveRange(0, Mathf.Min(3, totalThingModel.Count));
            }
            catch (Exception e)
            {
                // ignored
            }
        } 

        protected void SetIsSoldOut(bool isSoldOut)
        {
            _isSoldOut = isSoldOut;
        }

        protected virtual void ThingUpdateView()
        {
            foreach (var thingController in thingControllers)
            {
                thingController.UpdateAnimationView();
            }
        }

        public virtual List<((EThingType, EThingColorType) , Transform)> HammerItemRemoveTransforms((EThingType, EThingColorType) removeTypes)
        {
            var transforms = new List<((EThingType, EThingColorType) , Transform)>();
            for (int i = 0; i < totalThingModel.Count; i++)
            {
                if ((totalThingModel[i].type, totalThingModel[i].colorType) == removeTypes)
                {
                    var value = i % thingControllers.Count;
                    transforms.Add((removeTypes, thingControllers[value].transform));
                }
            }

            return transforms;
        }
        
        public virtual (bool, int) HammerItemRemoveType((EThingType, EThingColorType) removeTypes, int removeCount)
        {
            hammerRemoveCount = 0;

            foreach (var thing in totalThingModel)
            {
                if ((thing.type, thing.colorType) == removeTypes)
                {
                    if(removeCount > 3)
                        break;
                    
                    thing.type = EThingType.None;
                    thing.colorType = EThingColorType.None;

                    hammerRemoveCount++;
                    removeCount++;
                }
            }
            
            return (hammerRemoveCount > 0, removeCount);
        }

        public virtual void MagicWandItemChangeThing((EThingType, EThingColorType) changeThing, HashSet<(EThingType, EThingColorType)> changeThings)
        {
            if(changeThing.Item1 == EThingType.None)
                return;

            for (int i = 0; i < totalThingModel.Count; i++)
            {
                foreach (var thing in changeThings)
                {
                    if ((totalThingModel[i].type, totalThingModel[i].colorType) == thing)
                    {
                        totalThingModel[i].type = changeThing.Item1;
                        totalThingModel[i].colorType = changeThing.Item2;

                        if (i < thingControllers.Count)
                        {
                            thingControllers[i].gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        public List<((EThingType, EThingColorType), Transform)> MagicItemTransform(HashSet<(EThingType, EThingColorType)> changeThings)
        {
            var transforms = new List<((EThingType, EThingColorType), Transform)>();
            for (int i = 0; i < totalThingModel.Count; i++)
            {
                foreach (var thing in changeThings)
                {
                    if ((totalThingModel[i].type, totalThingModel[i].colorType) == thing)
                    {
                        var value = i % thingControllers.Count;
                        transforms.Add(((totalThingModel[i].type, totalThingModel[i].colorType), thingControllers[value].transform));
                        
                        if (i < thingControllers.Count)
                        {
                            thingControllers[i].gameObject.SetActive(false);
                        }
                    }
                }
            }

            return transforms;
        }
    }
}