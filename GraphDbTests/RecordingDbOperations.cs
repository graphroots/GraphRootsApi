using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphDbTests
{
    class RecordingDbOperations : IDbOperations
    {
        public List<object> MergedNodes { get; } = new List<object>();

        public List<object> CreatedNodes { get; } = new List<object>();

        public List<object> MergedRelationships { get; } = new List<object>();

        public int ExecuteWriteCount { get; private set; }

        public IReadOnlyList<T> NodesOfType<T>() => MergedNodes.OfType<T>().Concat(CreatedNodes.OfType<T>()).ToList();

        public IReadOnlyList<Tuple<TFrom, TTo, TRel>> RelationshipsOfType<TFrom, TTo, TRel>() =>
            MergedRelationships.OfType<Tuple<TFrom, TTo, TRel>>().ToList();

        public async Task ExecuteWrite(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteWriteCount++;
            await action(cancellationToken);
        }

        public Task CreateNode<T>(T node, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreatedNodes.Add(node!);
            return Task.CompletedTask;
        }

        public Task MergeNode<T>(T node, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergedNodes.Add(node!);
            return Task.CompletedTask;
        }

        public Task CreateNodes<T>(IEnumerable<T> node, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var n in node)
                CreatedNodes.Add(n!);
            return Task.CompletedTask;
        }

        public Task MergeNodes<T>(IEnumerable<T> node, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var n in node)
                MergedNodes.Add(n!);
            return Task.CompletedTask;
        }

        public Task MergeRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergedRelationships.Add(Tuple.Create(nodeFrom, nodeTo, relationship));
            return Task.CompletedTask;
        }

        public Task MergeRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var rel in relationships)
                MergedRelationships.Add(rel);
            return Task.CompletedTask;
        }

        public Task<T?> GetNode<T>(T key, CancellationToken cancellationToken = default) where T : class, new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FindNode(key));
        }

        public Task<List<T>> GetNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default) where T : class, new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = new List<T>();
            foreach (var key in keys)
            {
                var node = FindNode(key);
                if (node != null)
                    found.Add(node);
            }
            return Task.FromResult(found);
        }

        public Task DeleteNode<T>(T key, CancellationToken cancellationToken = default)
        {
            return DeleteNodes(new[] { key }, cancellationToken);
        }

        public Task DeleteNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var key in keys)
            {
                RemoveWhere(MergedNodes, key);
                RemoveWhere(CreatedNodes, key);
            }
            return Task.CompletedTask;
        }

        public Task DeleteRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default)
        {
            return DeleteRelationships(new[] { Tuple.Create(nodeFrom, nodeTo, relationship) }, cancellationToken);
        }

        public Task DeleteRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var rel in relationships)
                MergedRelationships.RemoveAll(existing => Equals(existing, rel));
            return Task.CompletedTask;
        }

        public Task PurgeDatabase(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergedNodes.Clear();
            CreatedNodes.Clear();
            MergedRelationships.Clear();
            return Task.CompletedTask;
        }

        T? FindNode<T>(T key) where T : class
        {
            foreach (var node in NodesOfType<T>())
            {
                if (EqualityMatch(node, key))
                    return node;
            }
            return null;
        }

        static void RemoveWhere<T>(List<object> list, T key)
        {
            list.RemoveAll(item => item is T typed && EqualityMatch(typed, key));
        }

        static bool EqualityMatch<T>(T left, T right)
        {
            foreach (var property in typeof(T).GetProperties())
            {
                if (!Attribute.IsDefined(property, typeof(DbEqualityCheckAttribute)))
                    continue;
                var a = property.GetValue(left);
                var b = property.GetValue(right);
                if (!Equals(a, b))
                    return false;
            }
            return true;
        }

        public Task CreateNodeIndices<Tnode>(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task VerifyConnectivity(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task EnsureSchema(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public List<string> Queries { get; } = new List<string>();

        public Task RunQuery<Trecord>(string query, object? parameters, Func<Trecord, Task> func, CancellationToken cancellationToken = default)
            where Trecord : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.CompletedTask;
        }

        public Task<List<Trecord>> RunQuery<Trecord>(string query, object? parameters = null, CancellationToken cancellationToken = default)
            where Trecord : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(new List<Trecord>());
        }

        public Task QueryRecords(string query, object? parameters, Func<IQueryRecord, Task> func, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.CompletedTask;
        }
    }
}
