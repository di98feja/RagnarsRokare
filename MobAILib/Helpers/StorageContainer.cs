using System.Linq;
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
            m_position = container.transform.position;
        }

        public StorageContainer(string uniqueId, Vector3 position)
        {
            UniqueId = uniqueId;
            m_position = position;
            Timestamp = 0f;
        }

        public StorageContainer(string uniqueId, float timeStamp)
        {
            Timestamp = timeStamp;
            UniqueId = uniqueId;
        }

        private Container m_container = null;
        private Vector3 m_position = Vector3.zero;

        public Container Container
        {
            get
            {
                if (m_container == null)
                {
                    try
                    {
                        m_container = Common.GetContainerById(UniqueId);
                    }
                    catch (System.Exception)
                    {
                        Common.Dbgl($"Failed GetContainerById({UniqueId})");
                    }
                }
                return m_container;
            }
        }

        public Vector3 Position
        {
            get
            {
                if (m_position == Vector3.zero)
                {
                    m_position = Container?.transform?.position ?? Vector3.zero;
                }
                return m_position;
            }
        }
        public string UniqueId { get; set; }
        public float Timestamp { get; set; }

        public string Serialize()
        {
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                return $"[{nameof(UniqueId)}:{UniqueId}][{nameof(m_position)}:{m_position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {m_position.y} {m_position.z}]";
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = currentCulture;
            }
        }

        public static StorageContainer DeSerialize(string s)
        {
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                Debug.Log($"Deserialize:{s}");
                var parts = s.SplitBySqBrackets();
                var uniqueId = parts.Where(p => p.Split(':')[0] == nameof(UniqueId)).Select(p => p.Split(':')[1]).Single();
                var position = parts.Where(p => p.Split(':')[0] == nameof(m_position))
                    .Select(p => p.Split(':')[1].Split(' '))
                    .Select(p => new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])))
                    .Single();

                Debug.Log($"Pos:{position}");
                return new StorageContainer(uniqueId, position);
            }
            catch (System.Exception)
            {
                Debug.Log($"Failed to deserialize");
                return null;
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = currentCulture;
            }
        }
    }
}
