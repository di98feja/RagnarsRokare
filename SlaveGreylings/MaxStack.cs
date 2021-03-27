﻿using System.Collections.Generic;
using System.Linq;

namespace SlaveGreylings
{
    public class MaxStack<T>
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
            return m_stack.Peek();
        }

        public void Clear()
        {
            m_stack.Clear();
        }

        public bool Contains(T item)
        {
            return m_stack.Contains(item);
        }
    }
}
