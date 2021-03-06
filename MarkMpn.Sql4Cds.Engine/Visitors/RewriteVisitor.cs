﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Linq;

namespace MarkMpn.Sql4Cds.Engine.Visitors
{
    /// <summary>
    /// Replaces expressions with the equivalent column names
    /// </summary>
    /// <remarks>
    /// During the post-processing of aggregate queries, a new schema is produced where the aggregates are stored in new
    /// columns. To make the processing of the remainder of the query easier, this class replaces any references to those
    /// aggregate functions with references to the calculated column name, e.g.
    /// SELECT firstname, count(*) FROM contact HAVING count(*) > 2
    /// would become
    /// SELECT firstname, agg1 FROM contact HAVING agg1 > 2
    /// 
    /// During query execution the agg1 column is generated from the aggregate query and allows the rest of the query execution
    /// to proceed without knowledge of how the aggregate was derived.
    /// </remarks>
    class RewriteVisitor : RewriteVisitorBase
    {
        private readonly IDictionary<string, string> _mappings;

        public RewriteVisitor(IDictionary<ScalarExpression,string> rewrites)
        {
            _mappings = rewrites.ToDictionary(kvp => kvp.Key.ToSql(), kvp => kvp.Value);
        }

        protected override ScalarExpression ReplaceExpression(ScalarExpression expression, out string name)
        {
            name = null;

            if (expression == null)
                return null;

            if (_mappings.TryGetValue(expression.ToSql(), out var column))
            {
                name = column;
                return new ColumnReferenceExpression
                {
                    MultiPartIdentifier = new MultiPartIdentifier
                    {
                        Identifiers =
                        {
                            new Identifier
                            {
                                Value = column
                            }
                        }
                    }
                };
            }

            return expression;
        }
    }
}
