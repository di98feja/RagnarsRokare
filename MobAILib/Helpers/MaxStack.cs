using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.MobAI
{
    public class MaxStack<T> : IEnumerable<T>
    {
        private Stack<T> m_stack;
        public int MaxSize { get; private set; }
        public MaxStack(int maxSize)
        {
            m_stack = new Stack<T>();
            MaxSize = maxSize;
        }

        public void Push(T item)
        {
            m_stack.Push(item);
            if (m_stack.Count > MaxSize)
            {
                var newStack = new Stack<T>(m_stack.ToArray().Reverse().Skip(1));
                m_stack = newStack;
            }
        }

        public T Pop()
        {
            return m_stack.Pop();
        }

        public T Peek()
        {
            if (m_stack.Any())
            {
                return m_stack.Peek();
            }
            return default(T);
        }

        public void Clear()
        {
            m_stack.Clear();
        }

        public bool Contains(T item)
        {
            return m_stack.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_stack.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_stack.GetEnumerator();
        }

        public void Remove(T item)
        {
            Stack<T> newstack = new Stack<T>();
            while (m_stack.Any())
            {
                T transfer = m_stack.Pop();
                if (!transfer.Equals(item))
                {
                    newstack.Push(transfer);
                }
            }
            m_stack = newstack;
        }
    }
}
