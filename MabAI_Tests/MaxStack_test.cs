using Microsoft.VisualStudio.TestTools.UnitTesting;
using RagnarsRokare.MobAI;
using System;

namespace MabAI_Tests
{
    [TestClass]
    public class MaxStack_Test
    {
        [TestMethod]
        public void Push_Peek_CorrectItem()
        {
            var ms = new MaxStack<int>(5);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            Assert.AreEqual(3, ms.Peek());
        }

        [TestMethod]
        public void Push_Pop_CorrectItem()
        {
            var ms = new MaxStack<int>(5);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            Assert.AreEqual(3, ms.Pop());
        }

        [TestMethod]
        public void Push_Pop_Peek_CorrectItem()
        {
            var ms = new MaxStack<int>(5);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            Assert.AreEqual(3, ms.Pop());
            Assert.AreEqual(2, ms.Peek());
        }

        [TestMethod]
        public void Push_Pop_Peek_All_CorrectItem()
        {
            var ms = new MaxStack<int>(5);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            Assert.AreEqual(3, ms.Peek());
            Assert.AreEqual(3, ms.Pop());
            Assert.AreEqual(2, ms.Peek());
            Assert.AreEqual(2, ms.Pop());
            Assert.AreEqual(1, ms.Peek());
            Assert.AreEqual(1, ms.Pop());
        }

        public void MaxSize_CorrectValue()
        {
            var ms = new MaxStack<int>(2);
            Assert.AreEqual(2, ms.MaxSize);
        }

        [TestMethod]
        public void Peek_WhenEmpty_returnDefault()
        {
            var ms1 = new MaxStack<int>(5);
            Assert.AreEqual(0, ms1.Peek());
            var ms2 = new MaxStack<string>(5);
            Assert.IsNull(ms2.Peek());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Pop_WhenEmpty_throwsInvalidOperationException()
        {
            var ms = new MaxStack<string>(5);
            ms.Pop();
        }

        [TestMethod]
        public void PushMoreThenMax_ExcessIsLost()
        {
            var ms = new MaxStack<string>(2);
            ms.Push("1");
            ms.Push("2");
            ms.Push("3");
            Assert.AreEqual("3", ms.Pop());
            Assert.AreEqual("2", ms.Pop());
            Assert.IsNull(ms.Peek());
        }

        [TestMethod]
        public void RemoveItem_GivesCorrectSequence()
        {
            var ms = new MaxStack<string>(3);
            ms.Push("1");
            ms.Push("2");
            ms.Push("3");
            ms.Remove("2");
            Assert.AreEqual("3", ms.Pop());
            Assert.AreEqual("1", ms.Pop());
            Assert.IsNull(ms.Peek());
        }

        [TestMethod]
        public void Contains_givesCorrectResult()
        {
            var ms = new MaxStack<string>(2);
            ms.Push("1");
            ms.Push("2");
            ms.Push("3");
            Assert.IsTrue(ms.Contains("3"));
            Assert.IsTrue(ms.Contains("2"));
            Assert.IsFalse(ms.Contains("1"));
        }

        [TestMethod]
        public void Clear_removeAll()
        {
            var ms = new MaxStack<string>(3);
            ms.Push("1");
            ms.Push("2");
            ms.Push("3");
            ms.Clear();
            Assert.IsFalse(ms.Contains("3"));
            Assert.IsFalse(ms.Contains("2"));
            Assert.IsFalse(ms.Contains("1"));
            Assert.AreEqual(3, ms.MaxSize);
        }

        [TestMethod]
        public void Shrink_GivesCorrectNewSize()
        {
            var ms = new MaxStack<string>(10);
            ms.Shrink(0.5f);
            Assert.AreEqual(5, ms.MaxSize);
        }

        [TestMethod]
        public void Grow_GivesCorrectNewSize()
        {
            var ms = new MaxStack<string>(10);
            ms.Grow(0.5f);
            Assert.AreEqual(15, ms.MaxSize);
        }

        [TestMethod]
        public void Shrink_ExcessIsLost()
        {
            var ms = new MaxStack<int>(4);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            ms.Push(4);
            ms.Shrink(0.5f);
            Assert.AreEqual(2, ms.MaxSize);
            Assert.AreEqual(4, ms.Pop());
            Assert.AreEqual(3, ms.Pop());
            Assert.AreEqual(0, ms.Peek());
        }

        [TestMethod]
        public void Grow_AddSpaceAtEnd()
        {
            var ms = new MaxStack<int>(2);
            ms.Push(1);
            ms.Push(2);
            ms.Grow(0.5f);
            Assert.AreEqual(3, ms.MaxSize);
            Assert.AreEqual(2, ms.Pop());
            Assert.AreEqual(1, ms.Pop());
            Assert.AreEqual(0, ms.Peek());
        }

        [TestMethod]
        public void SetSize_ExcessIsLost()
        {
            var ms = new MaxStack<int>(4);
            ms.Push(1);
            ms.Push(2);
            ms.Push(3);
            ms.Push(4);
            ms.MaxSize = 1;
            Assert.AreEqual(1, ms.MaxSize);
            Assert.AreEqual(4, ms.Pop());
        }
    }
}
