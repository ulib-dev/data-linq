using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;

namespace System.Data.Linq.Provider
{
    using System.Data.Linq.Mapping;

    public interface IDataServices
    {
        DataContext Context { get; }
        MetaModel Model { get; }
        IDeferredSourceFactory GetDeferredSourceFactory(MetaDataMember member);
        object GetCachedObject(Expression query);
        bool IsCachedObject(MetaType type, object instance);
        object InsertLookupCachedObject(MetaType type, object instance);
        void OnEntityMaterialized(MetaType type, object instance);
    }

    public interface IDeferredSourceFactory
    {
        IEnumerable CreateDeferredSource(object instance);
        IEnumerable CreateDeferredSource(object[] keyValues);
    }
}
