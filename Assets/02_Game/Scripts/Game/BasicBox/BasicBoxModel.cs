using System;
using Game.View;
using UnityEngine;

namespace Game.Model
{
    public enum EBoxType
    {
        None,
        Box,
        Gimmick,
    }

    [Serializable]
    public class BasicBoxModel
    {
        public int index;
        
        public EBoxType type;
        public Vector3 position;
        public int count;

        public Vector3 railPosition;

        public EBoxBody boxBody;
        
        public virtual void SetPosition(Vector3 pos)
        {
            position = pos;
        }

        public void SetType(EBoxType boxType)
        {
            type = boxType;
        }

        public override bool Equals(object obj)
        {
            if (obj is BasicBoxModel other) return position == other.position && type == other.type;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(position, type);
        }
    }
}