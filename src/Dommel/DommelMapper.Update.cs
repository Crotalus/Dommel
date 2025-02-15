using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;

namespace Dommel
{
    public static partial class DommelMapper
    {
        /// <summary>
        /// Updates the values of the specified entity in the database.
        /// The return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <param name="connection">The connection to the database. This can either be open or closed.</param>
        /// <param name="entity">The entity in the database.</param>
        /// <param name="transaction">Optional transaction for the command.</param>
        /// <returns>A value indicating whether the update operation succeeded.</returns>
        public static bool Update<TEntity>(this IDbConnection connection, TEntity entity, IDbTransaction transaction = null) where TEntity : class
        {
            var sql = BuildUpdateQuery<TEntity>(GetSqlBuilder(connection), typeof(TEntity), entity);
            LogQuery<TEntity>(sql);
            return connection.Execute(sql, entity, transaction) > 0;
        }

        /// <summary>
        /// Updates the values of the specified entity in the database.
        /// The return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <param name="connection">The connection to the database. This can either be open or closed.</param>
        /// <param name="entity">The entity in the database.</param>
        /// <param name="transaction">Optional transaction for the command.</param>
        /// <returns>A value indicating whether the update operation succeeded.</returns>
        public static async Task<bool> UpdateAsync<TEntity>(this IDbConnection connection, TEntity entity, IDbTransaction transaction = null) where TEntity : class
        {
            var sql = BuildUpdateQuery(GetSqlBuilder(connection), typeof(TEntity), entity);
            LogQuery<TEntity>(sql);
            return await connection.ExecuteAsync(sql, entity, transaction) > 0;
        }

        internal static string BuildUpdateQuery<TEntity>(ISqlBuilder sqlBuilder, Type type, TEntity entity = null) where TEntity : class
        {
            var cacheKey = new QueryCacheKey(QueryCacheType.Update, sqlBuilder, type);
            if (!QueryCache.TryGetValue(cacheKey, out var sql))
            {
                var tableName = Resolvers.Table(type, sqlBuilder);
                var keyProperties = Resolvers.KeyProperties(type);

                // Use all properties which are settable.
                var typeProperties = Resolvers.Properties(type)
                                              .Except(keyProperties)
                                              .Where(p => p.GetSetMethod() != null)
                                              .ToArray();

                var columnNames = typeProperties.Select(p =>
                {
                    var attr = p.GetCustomAttribute<ColumnAttribute>();
                    string param;
                    if(attr?.TypeName != null)
                         param = $"{sqlBuilder.PrefixParameter(p.Name)}::{attr.TypeName}";
                    else
                        param = sqlBuilder.PrefixParameter(p.Name);

                    return $"{Resolvers.Column(p, sqlBuilder)} = {param}";
                }).ToArray();
                var keyPropertyWhereClauses = keyProperties.Select(p => $"{Resolvers.Column(p, sqlBuilder)} = {sqlBuilder.PrefixParameter(p.Name)}");
                sql = $"update {tableName} set {string.Join(", ", columnNames)} where {string.Join(" and ", keyPropertyWhereClauses)}";

                QueryCache.TryAdd(cacheKey, sql);
            }

            return sql;
        }
    }
}
