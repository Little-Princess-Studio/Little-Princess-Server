using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;

namespace LPS.Core.Ipc
{
    /*
    Token sequence is a class which control the message waiting queue for Rpc calling.
    */
    public class TokenSequence<T> where T : IComparable
    {
        private Queue<T> queue_ = new();

        public bool Empty => queue_.Count == 0;

        public bool Check(T token)
        {
            return queue_.Peek().Equals(token);
        }

        public void Enqueue(T token)
        {
            queue_.Enqueue(token);
        }

        public T Dequeue()
        {
            return queue_.Dequeue();
        }
    }
};