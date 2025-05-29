using Cysharp.Threading.Tasks;
using Framework;
using I2.Loc;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Game.View
{
    public enum EBoxBody
    {
        box_none,
        box_full,
        box_down
    }
    
    public class BoxView : BasicBoxView
    {
        public TMP_Text boxLockCount;
        
        public TMP_Text boxCountText;
        public TMP_Text boxTypeText;
        public TMP_Text boxStartCountText;

        public SkeletonAnimation lockAnim;
        public ParticleSystem lockOpenParticle;
        
        public GameObject soldOutAnimObj;

        [Header("Combo")]
        public SpriteRenderer combo;
        public Animator comboAnimation;
        public Sprite[] comboImages;
        
        [Header("MapEditor")]
        public GameObject soldOutObj;
        public GameObject lockObj;
        public GameObject gravityObj;
        public GameObject startObj;
        
        private const string BOX_TOP = "box_top";
        private const string BOX_TOP_NONE = "none";
        
        private void Start()
        {
            comboAnimation.StopPlayback();
            lockOpenParticle.Stop();
        }

        public void SetBoxSprite(EBoxBody eBody) 
        {
            bodySpriteResolver.SetCategoryAndLabel(bodySpriteResolver.GetCategory(), eBody.ToString());
            topSpriteResolver.SetCategoryAndLabel(topSpriteResolver.GetCategory(), eBody == EBoxBody.box_down ? BOX_TOP_NONE : BOX_TOP);
        }

        public void SetBoxLockActive(bool isLock)
        {
            lockAnim.gameObject.SetActive(isLock);
        }

        public void SetBoxLockCount(int count)
        {
            if (count <= 0)
            {
                boxLockCount.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                boxLockCount.transform.parent.gameObject.SetActive(true);
                boxLockCount.text = $"{count}";
            }
        }
        
        public void SetBoxLockAnimation(string aniName, bool isLoop)
        {
            lockAnim.AnimationState.SetAnimation(0, aniName, isLoop);
        }

        public void AddBoxLockAnimation(string aniName)
        {
            lockAnim.AnimationState.AddAnimation(0, aniName, true, 0);
        }

        public void BoxLockOpenParticle()
        {
            lockOpenParticle.Play();
        }
        
        public void SoldOutAnimation()
        {
            soldOutAnimObj.SetActive(true);
        }
        
        public void SetSoldOut(bool isSoldOut)
        {
            soldOutObj.SetActive(isSoldOut);
        }

        public void Combo(int comboCount)
        {
            if(comboCount < 3)
                return;
            
            if (comboCount < 10)
            {
                combo.sprite = comboImages[comboCount - 3];
            }
            else
            {
                combo.sprite = comboImages[comboImages.Length - 1];
            }

            comboAnimation.Play("combo", 0, 0f);
        }

        public async UniTask LockUpdate(int boxOpenCount, bool isSound)
        {
            switch (boxOpenCount)
            {
                case 4:
                    SetBoxLockAnimation(ScriptLocalization.Animation.Lock_Idle_0, true);
                    if (isSound) SoundManager.Instance.PlaySFX(Address.UNLOCK_WAV);
                    break;
                case 3:
                    SetBoxLockAnimation(ScriptLocalization.Animation.Lock_Ani_1, false);
                    AddBoxLockAnimation(ScriptLocalization.Animation.Lock_Idle_1);
                    if (isSound) SoundManager.Instance.PlaySFX(Address.UNLOCK_WAV);
                    break;
                case 2:
                    SetBoxLockAnimation(ScriptLocalization.Animation.Lock_Ani_2, false);
                    AddBoxLockAnimation(ScriptLocalization.Animation.Lock_Idle_2);
                    if (isSound) SoundManager.Instance.PlaySFX(Address.UNLOCK_WAV);
                    break;
                case 1:
                    SetBoxLockAnimation(ScriptLocalization.Animation.Lock_Ani_3, false);
                    AddBoxLockAnimation(ScriptLocalization.Animation.Lock_Idle_3);
                    if (isSound) SoundManager.Instance.PlaySFX(Address.UNLOCK_WAV);
                    break;
                case 0:
                    SetBoxLockAnimation(ScriptLocalization.Animation.Lock_Ani_4, false);
                    await UniTask.WaitForSeconds(0.5f);
                    
                    if (isSound) SoundManager.Instance.PlaySFX(Address.UNLOCK_WAV);
                    BoxLockOpenParticle();
                    
                    await UniTask.WaitForSeconds(1f);
                    break;
            }
        }

        [Sirenix.OdinInspector.BoxGroup("Editor")]
        public int boxCount;

        [Sirenix.OdinInspector.BoxGroup("Editor")]
        public int boxStartCount;
        
        [Sirenix.OdinInspector.BoxGroup("Editor")]
        public bool isGravity;
        
        [Sirenix.OdinInspector.BoxGroup("Editor")]
        public EBoxBody boxBody;
        
        public void SetBoxCount(int count)
        {
#if UNITY_EDITOR
            boxCount = count;
            lockObj.SetActive(count > 0);
            boxCountText.text = $"{count}";
#endif
        }

        public void SetIsGravity(bool isBoxGravity)
        {
#if UNITY_EDITOR
            isGravity = isBoxGravity;
            gravityObj.SetActive(isBoxGravity);
#endif
        }

        public void SetBoxStartCount(int count)
        {
#if UNITY_EDITOR
            boxStartCount = count;
            startObj.SetActive(count > 0);
            boxStartCountText.text = $"{count}";
#endif
        }
    }
}