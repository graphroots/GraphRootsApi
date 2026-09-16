using System;

namespace GraphRoots.GraphDb;

public class GraphStoreException : Exception
{
    public GraphStoreException(string code, string message) : base(message)
    {
        Code = code;
    }

    public GraphStoreException(string code, string message, Exception inner) : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }

    public static class Codes
    {
        public const string NotFound = "NOT_FOUND";
        public const string ImmutableDocument = "IMMUTABLE_DOCUMENT";
        public const string InvalidPattern = "INVALID_PATTERN";
        public const string PatternTooLarge = "PATTERN_TOO_LARGE";
        public const string LimitRequired = "LIMIT_REQUIRED";
        public const string Conflict = "CONFLICT";
        public const string InvalidArgument = "INVALID_ARGUMENT";
        public const string MatchTimeout = "MATCH_TIMEOUT";
        public const string ResultTooLarge = "RESULT_TOO_LARGE";
        public const string StoreError = "STORE_ERROR";
    }

    public static GraphStoreException NotFound(string message) =>
        new(Codes.NotFound, message);

    public static GraphStoreException ImmutableDocument(string message) =>
        new(Codes.ImmutableDocument, message);

    public static GraphStoreException InvalidPattern(string message) =>
        new(Codes.InvalidPattern, message);

    public static GraphStoreException PatternTooLarge(string message) =>
        new(Codes.PatternTooLarge, message);

    public static GraphStoreException LimitRequired(string message) =>
        new(Codes.LimitRequired, message);

    public static GraphStoreException Conflict(string message) =>
        new(Codes.Conflict, message);

    public static GraphStoreException InvalidArgument(string message) =>
        new(Codes.InvalidArgument, message);

    public static GraphStoreException MatchTimeout(string message) =>
        new(Codes.MatchTimeout, message);

    public static GraphStoreException ResultTooLarge(string message) =>
        new(Codes.ResultTooLarge, message);

    public static GraphStoreException StoreError(string message, Exception? inner = null) =>
        inner == null ? new(Codes.StoreError, message) : new(Codes.StoreError, message, inner);
}
