using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.MobAI
{
    public class MaxStack<T> : IEnumerable<T>
    {
        private LinkedList<T> m_list;
        private int m_maxSize;
        public int MaxSize 
        { 
            get
            {
                return m_maxSize;
            }
            set
            {
                m_maxSize = value;
                while (m_list.Count > value)
                {
                    m_list.RemoveLast();
                }
            }
        }
        public MaxStack(int maxSize)
        {
            m_list = new LinkedList<T>();
            MaxSize = maxSize;
        }

        public void Push(T item)
        {
            m_list.AddFirst(item);
            if (m_list.Count > MaxSize)
            {
                m_list.RemoveLast();
            }
        }

        public T Pop()
        {
            var item = m_list.First();
            m_list.RemoveFirst();
            return item;
        }

        public T Peek()
        {
            return m_list.FirstOrDefault();
        }

        public void Clear()
        {
            m_list.Clear();
        }

        public bool Contains(T item)
        {
            return m_list.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        public void Remove(T item)
        {
            m_list.Remove(item);
        }

        /// <summary>
        /// Shrink MaxSize by a fraction.
        /// Given MaxSize of 10, a fraction of 0.9 gives new MaxSize of 9.
        /// Given MaxSize of 10, a fraction of 0.1 gives new MaxSize of 1.
        /// </summary>
        /// <param name="v"></param>
        public void Shrink(float fraction)
        {
            MaxSize = (int)(MaxSize * fraction);
            while (m_list.Count > MaxSize)
            {
                m_list.RemoveLast();
            }
        }

        /// Grow MaxSize by a fraction.
        /// Given MaxSize of 10, a fraction of 0.9 gives new MaxSize of 19.
        /// Given MaxSize of 10, a fraction of 0.1 gives new MaxSize of 11.
        public void Grow(float fraction)
        {
            MaxSize = (int)(MaxSize * (1.0 + fraction));
        }
    }
}
