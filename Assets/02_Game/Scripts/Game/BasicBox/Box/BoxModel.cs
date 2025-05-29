using System;
using UnityEngine;

namespace Game.Model
{
    [Serializable]
    public class BoxModel : BasicBoxModel
    {
        public int boxOpenCount;
        public EThingType boxOpenType;
        public bool isGravity;
        public int startCount;
        
        public bool _isSoldOut { get; private set; }
        
        public BoxModel(EBoxType type, Vector3 position)
        {
            this.type = type;
            this.position = position;
        }

        public void SetIsSoldOut(bool isSoldOut)
        {
            _isSoldOut = isSoldOut;
        }
    }
}