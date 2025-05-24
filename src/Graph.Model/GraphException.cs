// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents errors that occur during graph operations.
/// </summary>
/// <remarks>
/// This exception is thrown when general errors occur during graph access operations
/// such as entity creation, retrieval, update, deletion, or query execution.
/// </remarks>
[Serializable]
public class GraphException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public GraphException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphException"/> class with a specified error message 
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, 
    /// or a null reference if no inner exception is specified.</param>
    /// <remarks>
    /// The <see cref="Exception.InnerException"/> property can be used to retrieve the 
    /// original exception that caused this exception.
    /// </remarks>
    public GraphException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    protected GraphException(System.Runtime.Serialization.SerializationInfo info, 
                           System.Runtime.Serialization.StreamingContext context) 
        : base(info, context)
    {
    }
}