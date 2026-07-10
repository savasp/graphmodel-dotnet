// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents an error raised while translating a graph query expression.
/// </summary>
[Serializable]
public class GraphQueryTranslationException : GraphException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryTranslationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the translation failure.</param>
    public GraphQueryTranslationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryTranslationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the translation failure.</param>
    /// <param name="innerException">The exception that caused the translation failure.</param>
    public GraphQueryTranslationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
