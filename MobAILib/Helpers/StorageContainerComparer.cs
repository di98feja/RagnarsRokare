using System;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI.Helpers
{
    class StorageContainerComparer : IEqualityComparer<StorageContainer>
    {
        public bool Equals(StorageContainer x, StorageContainer y)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.UniqueId == y.UniqueId;
        }

        public int GetHashCode(StorageContainer obj)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(obj, null)) return 0;

            //Get hash code for the Name field if it is not null.
            return obj.UniqueId.GetHashCode();
        }
    }
}
