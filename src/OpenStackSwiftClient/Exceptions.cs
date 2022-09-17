using System;

namespace OpenStackSwiftClient
{
  public class OpenStackException : Exception
  {
    public OpenStackException() : base() {
    }

    public OpenStackException(string message) : base(message) {
    }

    public OpenStackException(string message, Exception innerException) : base(message, innerException) {
    }
  }

  public class OpenStackAuthorizationException : OpenStackException
  {
    public OpenStackAuthorizationException() : base("Unauthorized!") {
    }

    public OpenStackAuthorizationException(string message) : base(message) {
    }

    public OpenStackAuthorizationException(string message, Exception innerException) : base(message, innerException) {
    }
  }

  public class SwiftException : OpenStackException
  {
    public SwiftException(string message) : base(message) {
    }

    public SwiftException(string message, Exception innerException) : base(message, innerException) {
    }

    public SwiftException() {
    }
  }

  public class ObjectNotFoundException : SwiftException
  {
    public ObjectNotFoundException() {
    }

    public ObjectNotFoundException(string message) : base(message) {
    }

    public ObjectNotFoundException(string message, Exception innerException) : base(message, innerException) {
    }
  }

  public class ObjectAlreadyExistsException : SwiftException
  {
    public ObjectAlreadyExistsException() {
    }

    public ObjectAlreadyExistsException(string message) : base(message) {
    }

    public ObjectAlreadyExistsException(string message, Exception innerException) : base(message, innerException) {
    }
  }

  public class HashMismatchException : SwiftException
  {
    public HashMismatchException(string message) : base(message) {
    }

    public HashMismatchException(string message, Exception innerException) : base(message, innerException) {
    }

    public HashMismatchException() {
    }
  }
}