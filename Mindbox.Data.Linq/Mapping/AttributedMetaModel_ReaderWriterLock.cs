﻿//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Reflection;
//using System.Text;
//using System.Linq;
//using System.Data.Linq.SqlClient;
//using System.Threading;

//namespace System.Data.Linq.Mapping;

//internal class AttributedMetaModel : MetaModel
//{
//    ReaderWriterLock @lock = new ReaderWriterLock();
//    MappingSource mappingSource;
//    Type contextType;
//    Type providerType;
//    Dictionary<Type, MetaType> metaTypes;
//    Dictionary<Type, MetaTable> metaTables;
//    ReadOnlyCollection<MetaTable> staticTables;
//    Dictionary<MetaPosition, MetaFunction> metaFunctions;
//    string dbName;
//    bool initStaticTables;
//    bool initFunctions;

//    internal AttributedMetaModel(MappingSource mappingSource, Type contextType)
//    {
//        this.mappingSource = mappingSource;
//        this.contextType = contextType;
//        this.metaTypes = new Dictionary<Type, MetaType>();
//        this.metaTables = new Dictionary<Type, MetaTable>();
//        this.metaFunctions = new Dictionary<MetaPosition, MetaFunction>();

//        // Provider type
//        ProviderAttribute[] attrs = (ProviderAttribute[])this.contextType.GetCustomAttributes(typeof(ProviderAttribute), true);
//        if (attrs != null && attrs.Length == 1)
//        { // Provider attribute is !AllowMultiple
//            this.providerType = attrs[0].Type;
//        }
//        else
//        {
//            this.providerType = typeof(SqlProvider);
//        }

//        // Database name 
//        DatabaseAttribute[] das = (DatabaseAttribute[])this.contextType.GetCustomAttributes(typeof(DatabaseAttribute), false);
//        this.dbName = (das != null && das.Length > 0) ? das[0].Name : this.contextType.Name;
//    }

//    public override MappingSource MappingSource
//    {
//        get { return this.mappingSource; }
//    }

//    public override Type ContextType
//    {
//        get { return this.contextType; }
//    }

//    public override string DatabaseName
//    {
//        get { return this.dbName; }
//    }

//    public override Type ProviderType
//    {
//        get { return this.providerType; }
//    }

//    public override IEnumerable<MetaTable> GetTables()
//    {
//        this.InitStaticTables();
//        if (this.staticTables.Count > 0)
//        {
//            return this.staticTables;
//        }
//        else
//        {
//            @lock.AcquireReaderLock(Timeout.Infinite);
//            try
//            {
//                return this.metaTables.Values.Where(x => x != null).Distinct();
//            }
//            finally
//            {
//                @lock.ReleaseReaderLock();
//            }
//        }
//    }
//    #region Initialization
//    private void InitStaticTables()
//    {
//        if (!this.initStaticTables)
//        {
//            @lock.AcquireWriterLock(Timeout.Infinite);
//            try
//            {
//                if (!this.initStaticTables)
//                {
//                    HashSet<MetaTable> tables = new HashSet<MetaTable>();
//                    for (Type type = this.contextType; type != typeof(DataContext); type = type.BaseType)
//                    {
//                        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
//                        foreach (FieldInfo fi in fields)
//                        {
//                            Type ft = fi.FieldType;
//                            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(Table<>))
//                            {
//                                Type rowType = ft.GetGenericArguments()[0];
//                                tables.Add(this.GetTableNoLocks(rowType));
//                            }
//                        }
//                        PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
//                        foreach (PropertyInfo pi in props)
//                        {
//                            Type pt = pi.PropertyType;
//                            if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(Table<>))
//                            {
//                                Type rowType = pt.GetGenericArguments()[0];
//                                tables.Add(this.GetTableNoLocks(rowType));
//                            }
//                        }
//                    }
//                    this.staticTables = new List<MetaTable>(tables).AsReadOnly();
//                    this.initStaticTables = true;
//                }
//            }
//            finally
//            {
//                @lock.ReleaseWriterLock();
//            }
//        }
//    }

//    private void InitFunctions()
//    {
//        if (!this.initFunctions)
//        {
//            @lock.AcquireWriterLock(Timeout.Infinite);
//            try
//            {
//                if (!this.initFunctions)
//                {
//                    if (this.contextType != typeof(DataContext))
//                    {
//                        for (Type type = this.contextType; type != typeof(DataContext); type = type.BaseType)
//                        {
//                            foreach (MethodInfo mi in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
//                            {
//                                if (IsUserFunction(mi))
//                                {
//                                    if (mi.IsGenericMethodDefinition)
//                                    {
//                                        // Added this constraint because XML mapping model didn't support mapping sprocs to generic method.
//                                        // The attribute mapping model was, however, able to support it. This check is for parity between 
//                                        // the two models.
//                                        throw Error.InvalidUseOfGenericMethodAsMappedFunction(mi.Name);
//                                    }
//                                    MetaPosition mp = new MetaPosition(mi);
//                                    if (!this.metaFunctions.ContainsKey(mp))
//                                    {
//                                        MetaFunction metaFunction = new AttributedMetaFunction(this, mi);
//                                        this.metaFunctions.Add(mp, metaFunction);

//                                        // pre-set all known function result types into metaType map
//                                        foreach (MetaType rt in metaFunction.ResultRowTypes)
//                                        {
//                                            foreach (MetaType it in rt.InheritanceTypes)
//                                            {
//                                                if (!this.metaTypes.ContainsKey(it.Type))
//                                                {
//                                                    this.metaTypes.Add(it.Type, it);
//                                                }
//                                            }
//                                        }
//                                    }
//                                }
//                            }
//                        }
//                    }
//                    this.initFunctions = true;
//                }
//            }
//            finally
//            {
//                @lock.ReleaseWriterLock();
//            }
//        }
//    }

//    private static bool IsUserFunction(MethodInfo mi)
//    {
//        return Attribute.GetCustomAttribute(mi, typeof(FunctionAttribute), false) != null;
//    }
//    #endregion

//    public override MetaTable GetTable(Type rowType)
//    {
//        if (rowType == null)
//        {
//            throw Error.ArgumentNull("rowType");
//        }
//        MetaTable table;
//        @lock.AcquireReaderLock(Timeout.Infinite);
//        try
//        {
//            if (this.metaTables.TryGetValue(rowType, out table))
//            {
//                return table;
//            }
//        }
//        finally
//        {
//            @lock.ReleaseReaderLock();
//        }
//        @lock.AcquireWriterLock(Timeout.Infinite);
//        try
//        {
//            table = this.GetTableNoLocks(rowType);
//        }
//        finally
//        {
//            @lock.ReleaseWriterLock();
//        }
//        return table;
//    }

//    internal MetaTable GetTableNoLocks(Type rowType)
//    {
//        MetaTable table;
//        if (!this.metaTables.TryGetValue(rowType, out table))
//        {
//            Type root = GetRoot(rowType) ?? rowType;
//            TableAttribute[] attrs = (TableAttribute[])root.GetCustomAttributes(typeof(TableAttribute), true);
//            if (attrs.Length == 0)
//            {
//                this.metaTables.Add(rowType, null);
//            }
//            else
//            {
//                if (!this.metaTables.TryGetValue(root, out table))
//                {
//                    table = new AttributedMetaTable(this, attrs[0], root);
//                    foreach (MetaType mt in table.RowType.InheritanceTypes)
//                    {
//                        this.metaTables.Add(mt.Type, table);
//                    }
//                }
//                // catch case of derived type that is not part of inheritance
//                if (table.RowType.GetInheritanceType(rowType) == null)
//                {
//                    this.metaTables.Add(rowType, null);
//                    return null;
//                }
//            }
//        }
//        return table;
//    }

//    private static Type GetRoot(Type derivedType)
//    {
//        while (derivedType != null && derivedType != typeof(object))
//        {
//            TableAttribute[] attrs = (TableAttribute[])derivedType.GetCustomAttributes(typeof(TableAttribute), false);
//            if (attrs.Length > 0)
//                return derivedType;
//            derivedType = derivedType.BaseType;
//        }
//        return null;
//    }

//    public override MetaType GetMetaType(Type type)
//    {
//        if (type == null)
//        {
//            throw Error.ArgumentNull("type");
//        }
//        MetaType mtype = null;
//        @lock.AcquireReaderLock(Timeout.Infinite);
//        try
//        {
//            if (this.metaTypes.TryGetValue(type, out mtype))
//            {
//                return mtype;
//            }
//        }
//        finally
//        {
//            @lock.ReleaseReaderLock();
//        }
//        // Attributed meta model allows us to learn about tables we did not
//        // statically know about
//        MetaTable tab = this.GetTable(type);
//        if (tab != null)
//        {
//            return tab.RowType.GetInheritanceType(type);
//        }
//        this.InitFunctions();
//        @lock.AcquireWriterLock(Timeout.Infinite);
//        try
//        {
//            if (!this.metaTypes.TryGetValue(type, out mtype))
//            {
//                mtype = new UnmappedType(this, type);
//                this.metaTypes.Add(type, mtype);
//            }
//        }
//        finally
//        {
//            @lock.ReleaseWriterLock();
//        }
//        return mtype;
//    }

//    public override MetaFunction GetFunction(MethodInfo method)
//    {
//        if (method == null)
//        {
//            throw Error.ArgumentNull("method");
//        }
//        this.InitFunctions();
//        MetaFunction function = null;
//        this.metaFunctions.TryGetValue(new MetaPosition(method), out function);
//        return function;
//    }

//    //public override IEnumerable<MetaFunction> GetFunctions()
//    //{
//    //    this.InitFunctions();
//    //    return this.metaFunctions.Values.ToList().AsReadOnly();
//    //}

//    internal virtual AttributedRootType CreateRootType(AttributedMetaTable table, Type type)
//    {
//        return new AttributedRootType(this, table, type);
//    }

//    internal virtual IReadOnlyCollection<TableAttribute> GetTableAttributes(Type type, bool shouldInherit)
//    {
//        if (type == null)
//            throw new ArgumentNullException("type");

//        return (TableAttribute[])type.GetCustomAttributes(typeof(TableAttribute), shouldInherit);
//    }

//    internal virtual ColumnAttribute TryGetColumnAttribute(MemberInfo member)
//    {
//        if (member == null)
//            throw new ArgumentNullException("member");

//        return (ColumnAttribute)Attribute.GetCustomAttribute(member, typeof(ColumnAttribute));
//    }

//    internal virtual AssociationAttribute TryGetAssociationAttribute(MemberInfo member)
//    {
//        if (member == null)
//            throw new ArgumentNullException("member");

//        return (AssociationAttribute)Attribute.GetCustomAttribute(member, typeof(AssociationAttribute));
//    }

//    internal virtual bool IsDeferredMember(MemberInfo member, Type storageType, AssociationAttribute associationAttribute)
//    {
//        if (member == null)
//            throw new ArgumentNullException("member");
//        if (storageType == null)
//            throw new ArgumentNullException("storageType");

//        return IsDeferredType(storageType);
//    }

//    internal static bool IsDeferredType(Type entityType)
//    {
//        if (entityType == null || entityType == typeof(object))
//            return false;

//        if (!entityType.IsGenericType)
//            return false;

//        var genericTypeDefinition = entityType.GetGenericTypeDefinition();
//        return genericTypeDefinition == typeof(Link<>) ||
//            typeof(EntitySet<>).IsAssignableFrom(genericTypeDefinition) ||
//            typeof(EntityRef<>).IsAssignableFrom(genericTypeDefinition) ||
//            IsDeferredType(entityType.BaseType);
//    }
//}