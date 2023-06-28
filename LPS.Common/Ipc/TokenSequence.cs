// -----------------------------------------------------------------------
// <copyright file="TokenSequence.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

/// <summary>
/// Token sequence is a class which control the message waiting queue for Rpc calling.
/// </summary>
/// <typeparam name="T">Type of token.</typeparam>
public class TokenSequence<T>
    where T : IComparable
{
    private readonly Queue<T> queue = new();

    /// <summary>
    /// Gets a value indicating whether the token sequence is empty.
    /// </summary>
    public bool Empty => this.queue.Count == 0;

    /// <summary>
    /// Check if the token equals to the first token in the sequence.
    /// </summary>
    /// <param name="token">Token.</param>
    /// <returns>If the token equals to the first token in the sequence.</returns>
    public bool Check(T token)
    {
        return this.queue.Peek().Equals(token);
    }

    /// <summary>
    /// Enqueue a token to the sequence.
    /// </summary>
    /// <param name="token">Token.</param>
    public void Enqueue(T token)
    {
        this.queue.Enqueue(token);
    }

    /// <summary>
    /// Dequeue a token from the sequence.
    /// </summary>
    /// <returns>Token.</returns>
    public T Dequeue()
    {
        return this.queue.Dequeue();
    }
}