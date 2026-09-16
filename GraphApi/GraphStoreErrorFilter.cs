using System;
using GraphRoots.GraphDb;
using HotChocolate;

namespace GraphRoots.GraphApi;

public sealed class GraphStoreErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        for (var ex = error.Exception; ex != null; ex = ex.InnerException)
        {
            if (ex is GraphStoreException store)
            {
                return error
                    .WithMessage(store.Message)
                    .WithCode(store.Code);
            }
        }

        if (error.Exception != null)
        {
            return error
                .WithMessage(error.Exception.Message)
                .WithCode(GraphStoreException.Codes.StoreError);
        }

        return error;
    }
}
