using System;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI.Helpers
{
    class ItemDataComparer : IEqualityComparer<ItemDrop.ItemData>
    {
        public bool Equals(ItemDrop.ItemData x, ItemDrop.ItemData y)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.m_shared.m_name == y.m_shared.m_name;
        }

        public int GetHashCode(ItemDrop.ItemData obj)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(obj, null)) return 0;

            //Get hash code for the Name field if it is not null.
            return obj.m_shared.m_name.GetHashCode();
        }
    }
}
