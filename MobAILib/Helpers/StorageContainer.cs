namespace RagnarsRokare.MobAI
{
    public class StorageContainer
    {
        public StorageContainer(Container container)
        {
            m_container = container;
            UniqueId = Common.GetOrCreateUniqueId(Common.GetNView(container));
        }

        public StorageContainer(string containerUniqueId)
        {
            UniqueId = containerUniqueId;
        }

        private Container m_container = null;

        public Container Container 
        {
            get
            {
                if (m_container == null)
                {
                    Common.GetContainerById(UniqueId);
                }
                return m_container;
            } 
        }

        public string UniqueId { get; }
        public ZNetView NView { get { return Common.GetNView(Container); } }
    }
}
