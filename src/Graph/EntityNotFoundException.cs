// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents graph operation failures caused by a requested entity not being found.
/// </summary>
/// <remarks>
/// This exception is thrown for graph operations that require an existing node or relationship,
/// such as retrieval, update, or deletion by ID.
/// </remarks>
[Serializable]
public class EntityNotFoundException : GraphException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains which entity was not found.</param>
    public EntityNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityNotFoundException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains which entity was not found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public EntityNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
