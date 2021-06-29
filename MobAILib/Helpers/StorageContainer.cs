using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class StorageContainer
    {
        public StorageContainer(Container container, float timeStamp)
        {
            m_container = container;
            Timestamp = timeStamp;
            UniqueId = Common.GetOrCreateUniqueId(Common.GetNView(m_container));
            Position = container.transform.position;
        }

        public StorageContainer(string uniqueId, float timeStamp)
        {
            Timestamp = timeStamp;
            UniqueId = uniqueId;
        }

        private Container m_container = null;

        public Container Container 
        {
            get
            {
                if (m_container == null)
                {
                    m_container = Common.GetContainerById(UniqueId);
                    Position = m_container?.transform.position ?? Vector3.zero;
                }
                return m_container;
            } 
        }

        public Vector3 Position { get; set; }
        public string UniqueId { get; set; }
        public float Timestamp { get; set; }
    }
}
