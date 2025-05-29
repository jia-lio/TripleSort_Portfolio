using System;
using App;
using Framework.Addressable;
using Framework.Scripts.NetClient;
using Framework.Utility;
using Game.Model;
using Game.StageProgress;
using Newtonsoft.Json;
using UnityEngine.AddressableAssets;

namespace Game.System
{
    public class UserSystem
    {
        [Serializable]
        public class Option
        {
            [JsonProperty("S")] public int stage;
            [JsonProperty("SU")] public int successive;
            [JsonProperty("SC")] public int stageClearCount;

            [JsonProperty("IC")] public bool isStageClear;
        }
        
        private SyncAssetOption<Option> _stageOption;

        public bool IsTutorial => CurrentStage <= 0 && IsTestMode == false;
        public bool IsStartHammerItem { get; set; }
        public bool IsStartTimeItem { get; set; }
        public bool IsStartStarDoubleItem { get; set; }
        public bool IsAdFullBooster { get; set; }
        
        public static bool IsTestMode { get; set; }
        
        private const int MAX_STAGE = 350;
        
        private StageProgressSystem _roomSystem;

        public void Init()
        {
            _stageOption = new SyncAssetOption<Option>(ASSET.STAGE);
        }

        public int CurrentStage
        {
#if UNITY_EDITOR
            get => IsTestMode ? 0 : _stageOption.Value.stage;
#else
            get => _stageOption.Value.stage;
#endif
            private set
            {
                _stageOption.Value.stage = value;
                _stageOption.Update();
            }
        }

        public int CurrentSuccessive
        {
            get => _stageOption.Value.successive;
            private set
            {
                _stageOption.Value.successive = value;
                _stageOption.Update();
            }
        }

        public int CurrentStageClearCount
        {
            get => _stageOption.Value.stageClearCount;
            private set
            {
                _stageOption.Value.stageClearCount = value;
                _stageOption.Update();
            }
        }
        
        public StageModel GetCurrentStage()
        {
            StageModel result;
            
#if UNITY_EDITOR
            if (IsTestMode)
            {
                result = Addressables.LoadAssetAsync<StageModel>($"Stages/test.asset").WaitForCompletion();
                result.Init();
                return result;
            }
#endif
            result = GetStage(CurrentStage);
            result.Init();
            return result;
        }

        public StageModel GetStage(int stage)
        {
            try
            {
                return AddressableCache.Load<StageModel>(GetStageKey(stage + 1));
            }
            catch
            {
                //ignored
            }

            return null;
        }

        public void StageClear()
        {
            if(IsTestMode)
                return;
            
            _stageOption.Value.isStageClear = true;
            _stageOption.Update();

            
            CurrentSuccessive++;
            CurrentStage++;

            AppsFlyerUtility.Stage(CurrentStage - 1);
            
            FirebaseController.ClearStage(CurrentStage - 1);
            FirebaseController.StageSuccessive(CurrentSuccessive);

            _roomSystem.Asset.Add(GetPrevStage().GetRoomKey());
            
            BoosterItemClear();
        }

        public void StageClearCount()
        {
            if (IsTestMode) 
                return;

            if (CurrentStageClearCount >= 50)
            {
                CurrentStageClearCount = 0;
            }
            
            CurrentStageClearCount += 1;
        }
        
        public bool IsStageClear()
        {
            if (_stageOption.Value.isStageClear == false) 
                return false;
            
            _stageOption.Value.isStageClear = false;
            _stageOption.Update();
            return true;
        }
        
        public void StageFailed() 
        {
            CurrentSuccessive = 0;
            BoosterItemClear();
            
            FirebaseController.FailStage(CurrentStage);
        }
        
        private string GetStageKey(int stage)
        {
            return $"Stages/{stage}.asset";
        }

        public void SetStageProgressSystem(StageProgressSystem system)
        {
            _roomSystem = system;
        }

        public StageModel GetPrevStage()
        {
            return GetStage(CurrentStage - 1);
        }

        public int GetSuccessiveTime()
        {
            if (CurrentSuccessive <= 0)
                return 0;

            return CurrentSuccessive switch
            {
                1 => 10,
                2 => 20,
                >= 3 => 30
            };
        }

        public void BoosterItemClear()
        {
            IsStartHammerItem = false;
            IsStartTimeItem = false;
            IsStartStarDoubleItem = false;
        }
        
        public bool IsLastStage()
        {
            return _stageOption.Value.stage >= MAX_STAGE;
        }
    }
}