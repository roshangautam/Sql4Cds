﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using FakeXrmEasy;
using FakeXrmEasy.FakeMessageExecutors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.Sql4Cds.Engine.Tests
{
    [TestClass]
    public class Sql2FetchXmlTests : IQueryExecutionOptions
    {
        bool IQueryExecutionOptions.Cancelled => false;

        bool IQueryExecutionOptions.BlockUpdateWithoutWhere => false;

        bool IQueryExecutionOptions.BlockDeleteWithoutWhere => false;

        bool IQueryExecutionOptions.UseBulkDelete => false;

        int IQueryExecutionOptions.BatchSize => 1;

        bool IQueryExecutionOptions.UseTSQLEndpoint => false;

        [TestMethod]
        public void SimpleSelect()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void SelectSameFieldMultipleTimes()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");

            CollectionAssert.AreEqual(new[]
            {
                "accountid",
                "name",
                "name"
            }, ((SelectQuery)queries[0]).ColumnSet);
        }

        [TestMethod]
        public void SelectStar()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT * FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <all-attributes />
                    </entity>
                </fetch>
            ");

            CollectionAssert.AreEqual(new[]
            {
                "accountid",
                "createdon",
                "name",
                "primarycontactid"
            }, ((SelectQuery)queries[0]).ColumnSet);
        }

        [TestMethod]
        public void SelectStarAndField()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT *, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <all-attributes />
                    </entity>
                </fetch>
            ");

            CollectionAssert.AreEqual(new[]
            {
                "accountid",
                "createdon",
                "name",
                "primarycontactid",
                "name"
            }, ((SelectQuery)queries[0]).ColumnSet);
        }

        [TestMethod]
        public void SimpleFilter()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account WHERE name = 'test'";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='test' />
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void FetchFilter()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contactid, firstname FROM contact WHERE createdon = lastxdays(7)";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='contactid' />
                        <attribute name='firstname' />
                        <filter>
                            <condition attribute='createdon' operator='last-x-days' value='7' />
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void NestedFilters()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account WHERE name = 'test' OR (accountid is not null and name like 'foo%')";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <filter type='or'>
                            <condition attribute='name' operator='eq' value='test' />
                            <filter type='and'>
                                <condition attribute='accountid' operator='not-null' />
                                <condition attribute='name' operator='like' value='foo%' />
                            </filter>
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void Sorts()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account ORDER BY name DESC, accountid";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <order attribute='name' descending='true' />
                        <order attribute='accountid' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void SortByColumnIndex()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account ORDER BY 2 DESC, 1";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <order attribute='name' descending='true' />
                        <order attribute='accountid' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void SortByAliasedColumn()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name as accountname FROM account ORDER BY name";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' alias='accountname' />
                        <attribute name='name' />
                        <order attribute='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void Top()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT TOP 10 accountid, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch top='10'>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void TopBrackets()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT TOP (10) accountid, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch top='10'>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void NoLock()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account (NOLOCK)";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch no-lock='true'>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void Distinct()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT DISTINCT accountid, name FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch distinct='true'>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void Offset()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account ORDER BY name OFFSET 100 ROWS FETCH NEXT 50 ROWS ONLY";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch count='50' page='3'>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <order attribute='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void SimpleJoin()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account INNER JOIN contact ON accountid = parentcustomerid";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner' alias='contact'>
                        </link-entity>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void SelfReferentialJoin()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contact.contactid, contact.firstname, manager.firstname FROM contact LEFT OUTER JOIN contact AS manager ON contact.parentcustomerid = manager.contactid";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='contactid' />
                        <attribute name='firstname' />
                        <link-entity name='contact' from='contactid' to='parentcustomerid' link-type='outer' alias='manager'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void AdditionalJoinCriteria()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account INNER JOIN contact ON accountid = parentcustomerid AND (firstname = 'Mark' OR lastname = 'Carrington')";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner' alias='contact'>
                            <filter type='or'>
                                <condition attribute='firstname' operator='eq' value='Mark' />
                                <condition attribute='lastname' operator='eq' value='Carrington' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedQueryFragmentException))]
        public void InvalidAdditionalJoinCriteria()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account INNER JOIN contact ON accountid = parentcustomerid OR (firstname = 'Mark' AND lastname = 'Carrington')";

            sql2FetchXml.Convert(query);
        }

        [TestMethod]
        public void SortOnLinkEntity()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account INNER JOIN contact ON accountid = parentcustomerid ORDER BY name, firstname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner' alias='contact'>
                            <order attribute='firstname' />
                        </link-entity>
                        <order attribute='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void InvalidSortOnLinkEntity()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT accountid, name FROM account INNER JOIN contact ON accountid = parentcustomerid ORDER BY firstname, name";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='accountid' />
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' alias='contact' link-type='inner'>
                            <attribute name='firstname' />
                            <order attribute='firstname' />
                        </link-entity>
                    </entity>
                </fetch>
            ");

            Assert.AreEqual(1, ((FetchXmlQuery)queries[0]).Extensions.Count);
        }

        [TestMethod]
        public void SimpleAggregate()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT count(*), count(name), count(DISTINCT name), max(name), min(name), avg(name) FROM account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='accountid' aggregate='count' alias='accountid_count' />
                        <attribute name='name' aggregate='countcolumn' alias='name_count' />
                        <attribute name='name' aggregate='countcolumn' distinct='true' alias='name_count_2' />
                        <attribute name='name' aggregate='max' alias='name_max' />
                        <attribute name='name' aggregate='min' alias='name_min' />
                        <attribute name='name' aggregate='avg' alias='name_avg' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void GroupBy()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name, count(*) FROM account GROUP BY name";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='accountid_count' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void GroupBySorting()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name, count(*) FROM account GROUP BY name ORDER BY name, count(*)";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='accountid_count' />
                        <order alias='name' />
                        <order alias='accountid_count' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void GroupBySortingOnLinkEntity()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name, firstname, count(*) FROM account INNER JOIN contact ON parentcustomerid = account.accountid GROUP BY name, firstname ORDER BY name, firstname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='accountid_count' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner' alias='contact'>
                            <attribute name='firstname' groupby='true' alias='firstname' />
                            <order alias='firstname' />
                        </link-entity>
                        <order alias='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void GroupBySortingOnAliasedAggregate()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name, firstname, count(*) as count FROM account INNER JOIN contact ON parentcustomerid = account.accountid GROUP BY name, firstname ORDER BY count";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='count' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner' alias='contact'>
                            <attribute name='firstname' groupby='true' alias='firstname' />
                        </link-entity>
                        <order alias='count' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void UpdateFieldToValue()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "UPDATE contact SET firstname = 'Mark'";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch distinct='true'>
                    <entity name='contact'>
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void WhereComparingTwoFields()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contactid FROM contact WHERE firstname = lastname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Carrington"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Mark"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
        }

        [TestMethod]
        public void WhereComparingExpression()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contactid FROM contact WHERE lastname = firstname + 'rington'";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Car",
                    ["lastname"] = "Carrington"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Mark"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid1, ((EntityCollection)queries[0].Result).Entities[0].Id);
        }

        [TestMethod]
        public void BackToFrontLikeExpression()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contactid FROM contact WHERE 'Mark' like firstname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Foo"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "M%"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
        }

        [TestMethod]
        public void UpdateFieldToField()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "UPDATE contact SET firstname = lastname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch distinct='true'>
                    <entity name='contact'>
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            var guid = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid] = new Entity("contact", guid)
                {
                    ["contactid"] = guid,
                    ["lastname"] = "Carrington"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual("Carrington", context.Data["contact"][guid]["firstname"]);
        }

        [TestMethod]
        public void UpdateFieldToExpression()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "UPDATE contact SET firstname = 'Hello ' + lastname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch distinct='true'>
                    <entity name='contact'>
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            var guid = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid] = new Entity("contact", guid)
                {
                    ["contactid"] = guid,
                    ["lastname"] = "Carrington"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual("Hello Carrington", context.Data["contact"][guid]["firstname"]);
        }

        [TestMethod]
        public void UpdateReplace()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "UPDATE contact SET firstname = REPLACE(firstname, 'Dataflex Pro', 'CDS') WHERE lastname = 'Carrington'";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch distinct='true'>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='contactid' />
                        <filter>
                            <condition attribute='lastname' operator='eq' value='Carrington' />
                        </filter>
                    </entity>
                </fetch>
            ");

            var guid = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid] = new Entity("contact", guid)
                {
                    ["contactid"] = guid,
                    ["firstname"] = "--Dataflex Pro--",
                    ["lastname"] = "Carrington"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual("--CDS--", context.Data["contact"][guid]["firstname"]);
        }

        [TestMethod]
        public void SelectExpression()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, 'Hello ' + firstname AS greeting FROM contact";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                    </entity>
                </fetch>
            ");

            var guid = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid] = new Entity("contact", guid)
                {
                    ["contactid"] = guid,
                    ["firstname"] = "Mark"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual("Mark", ((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<string>("firstname"));
            Assert.AreEqual("Hello Mark", ((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<string>("greeting"));
        }

        [TestMethod]
        public void SelectExpressionNullValues()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, 'Hello ' + firstname AS greeting, case when createdon > '2020-01-01' then 'new' else 'old' end AS age FROM contact";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='createdon' />
                    </entity>
                </fetch>
            ");

            var guid = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid] = new Entity("contact", guid)
                {
                    ["contactid"] = guid
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.IsNull(((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<string>("firstname"));
            Assert.IsNull(((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<string>("greeting"));
            Assert.AreEqual("old", ((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<string>("age"));
        }

        [TestMethod]
        public void OrderByExpression()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, lastname FROM contact ORDER BY lastname + ', ' + firstname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Carrington"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "Data",
                    ["lastname"] = "8"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(2, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
            Assert.AreEqual(guid1, ((EntityCollection)queries[0].Result).Entities[1].Id);
        }

        [TestMethod]
        public void OrderByCalculatedField()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, lastname, lastname + ', ' + firstname AS fullname FROM contact ORDER BY fullname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Carrington"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "Data",
                    ["lastname"] = "8"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(2, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
            Assert.AreEqual(guid1, ((EntityCollection)queries[0].Result).Entities[1].Id);
        }

        [TestMethod]
        public void OrderByCalculatedFieldByIndex()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, lastname, lastname + ', ' + firstname AS fullname FROM contact ORDER BY 3";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["firstname"] = "Mark",
                    ["lastname"] = "Carrington"
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["firstname"] = "Data",
                    ["lastname"] = "8"
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(2, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
            Assert.AreEqual(guid1, ((EntityCollection)queries[0].Result).Entities[1].Id);
        }

        [TestMethod]
        public void DateCalculations()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT DATEADD(day, 1, createdon) AS nextday, DATEPART(minute, createdon) AS minute FROM contact WHERE DATEDIFF(hour, '2020-01-01', createdon) < 1";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='createdon' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["contactid"] = guid1,
                    ["createdon"] = new DateTime(2020, 2, 1)
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["contactid"] = guid2,
                    ["createdon"] = new DateTime(2020, 1, 1, 0, 30, 0)
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(1, ((EntityCollection)queries[0].Result).Entities.Count);
            Assert.AreEqual(guid2, ((EntityCollection)queries[0].Result).Entities[0].Id);
            Assert.AreEqual(new DateTime(2020, 1, 2, 0, 30, 0), ((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<DateTime>("nextday"));
            Assert.AreEqual(30, ((EntityCollection)queries[0].Result).Entities[0].GetAttributeValue<int>("minute"));
        }

        [TestMethod]
        public void TopAppliedAfterCustomFilter()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT TOP 10 contactid FROM contact WHERE firstname = lastname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                    </entity>
                </fetch>
            ");

            Assert.AreEqual(2, ((FetchXmlQuery)queries[0]).Extensions.Count);
        }

        [TestMethod]
        public void CustomFilterAggregateHavingProjectionSortAndTop()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT TOP 10 lastname, SUM(CASE WHEN firstname = 'Mark' THEN 1 ELSE 0 END) as nummarks, LEFT(lastname, 1) AS lastinitial FROM contact WHERE DATEDIFF(day, '2020-01-01', createdon) > 10 GROUP BY lastname HAVING count(*) > 1 ORDER BY 2 DESC";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='createdon' />
                        <attribute name='lastname' />
                        <attribute name='firstname' />
                        <attribute name='contactid' />
                        <order attribute='lastname' />
                    </entity>
                </fetch>
            ");

            Assert.AreEqual(6, ((FetchXmlQuery)queries[0]).Extensions.Count);

            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [Guid.NewGuid()] = new Entity("contact")
                {
                    ["lastname"] = "Carrington",
                    ["firstname"] = "Mark",
                    ["createdon"] = DateTime.Parse("2020-01-01") // Ignored by WHERE
                },
                [Guid.NewGuid()] = new Entity("contact")
                {
                    ["lastname"] = "Carrington",
                    ["firstname"] = "Mark", // nummarks = 1
                    ["createdon"] = DateTime.Parse("2020-02-01") // Included by WHERE
                },
                [Guid.NewGuid()] = new Entity("contact")
                {
                    ["lastname"] = "Carrington", // Included by HAVING count(*) > 1
                    ["firstname"] = "Matt", // nummarks = 1
                    ["createdon"] = DateTime.Parse("2020-02-01") // Included by WHERE
                },
                [Guid.NewGuid()] = new Entity("contact")
                {
                    ["lastname"] = "Doe",
                    ["firstname"] = "Mark", // nummarks = 1
                    ["createdon"] = DateTime.Parse("2020-02-01") // Included by WHERE
                },
                [Guid.NewGuid()] = new Entity("contact")
                {
                    ["lastname"] = "Doe", // Included by HAVING count(*) > 1
                    ["firstname"] = "Mark", // nummarks = 2
                    ["createdon"] = DateTime.Parse("2020-02-01") // Included by WHERE
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)queries[0].Result;
            Assert.AreEqual(2, results.Entities.Count);

            Assert.AreEqual("Doe", results.Entities[0].GetAttributeValue<string>("lastname"));
            Assert.AreEqual(2, results.Entities[0].GetAttributeValue<int>("nummarks"));
            Assert.AreEqual("D", results.Entities[0].GetAttributeValue<string>("lastinitial"));

            Assert.AreEqual("Carrington", results.Entities[1].GetAttributeValue<string>("lastname"));
            Assert.AreEqual(1, results.Entities[1].GetAttributeValue<int>("nummarks"));
            Assert.AreEqual("C", results.Entities[1].GetAttributeValue<string>("lastinitial"));
        }

        [TestMethod]
        public void FilterCaseInsensitive()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT contactid FROM contact WHERE DATEDIFF(day, '2020-01-01', createdon) < 10 OR lastname = 'Carrington' ORDER BY createdon";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='createdon' />
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                        <order attribute='createdon' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var guid3 = Guid.NewGuid();

            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["lastname"] = "Carrington",
                    ["firstname"] = "Mark",
                    ["createdon"] = DateTime.Parse("2020-02-01"),
                    ["contactid"] = guid1
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["lastname"] = "CARRINGTON",
                    ["firstname"] = "Mark",
                    ["createdon"] = DateTime.Parse("2020-03-01"),
                    ["contactid"] = guid2
                },
                [guid3] = new Entity("contact", guid3)
                {
                    ["lastname"] = "Bloggs",
                    ["firstname"] = "Joe",
                    ["createdon"] = DateTime.Parse("2020-04-01"),
                    ["contactid"] = guid3
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)queries[0].Result;
            Assert.AreEqual(2, results.Entities.Count);

            Assert.AreEqual(guid1, results.Entities[0].Id);
            Assert.AreEqual(guid2, results.Entities[1].Id);
        }

        [TestMethod]
        public void GroupCaseInsensitive()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT lastname, count(*) FROM contact WHERE DATEDIFF(day, '2020-01-01', createdon) > 10 GROUP BY lastname ORDER BY 2 DESC";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='createdon' />
                        <attribute name='lastname' />
                        <attribute name='contactid' />
                        <order attribute='lastname' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var guid3 = Guid.NewGuid();

            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["lastname"] = "Carrington",
                    ["firstname"] = "Mark",
                    ["createdon"] = DateTime.Parse("2020-02-01"),
                    ["contactid"] = guid1
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["lastname"] = "CARRINGTON",
                    ["firstname"] = "Mark",
                    ["createdon"] = DateTime.Parse("2020-03-01"),
                    ["contactid"] = guid2
                },
                [guid3] = new Entity("contact", guid3)
                {
                    ["lastname"] = "Bloggs",
                    ["firstname"] = "Joe",
                    ["createdon"] = DateTime.Parse("2020-04-01"),
                    ["contactid"] = guid3
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)queries[0].Result;
            Assert.AreEqual(2, results.Entities.Count);

            Assert.AreEqual("Carrington", results.Entities[0].GetAttributeValue<string>("lastname"), true);
            Assert.AreEqual("BLoggs", results.Entities[1].GetAttributeValue<string>("lastname"), true);
        }

        [TestMethod]
        public void AggregateExpressionsWithoutGrouping()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT count(DISTINCT firstname + ' ' + lastname) AS distinctnames FROM contact";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                    </entity>
                </fetch>
            ");

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var guid3 = Guid.NewGuid();

            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("contact", guid1)
                {
                    ["lastname"] = "Carrington",
                    ["firstname"] = "Mark",
                    ["contactid"] = guid1
                },
                [guid2] = new Entity("contact", guid2)
                {
                    ["lastname"] = "CARRINGTON",
                    ["firstname"] = "Mark",
                    ["contactid"] = guid2
                },
                [guid3] = new Entity("contact", guid3)
                {
                    ["lastname"] = "Bloggs",
                    ["firstname"] = "Joe",
                    ["contactid"] = guid3
                }
            };

            queries[0].Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)queries[0].Result;
            Assert.AreEqual(1, results.Entities.Count);

            Assert.AreEqual(2, results.Entities[0].GetAttributeValue<int>("distinctnames"));
        }

        [TestMethod]
        public void AggregateQueryProducesAlternative()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name, count(*) FROM account GROUP BY name ORDER BY 2 DESC";

            var queries = sql2FetchXml.Convert(query);

            var simpleAggregate = (SelectQuery)queries[0];
            var alterativeQuery = (SelectQuery)simpleAggregate.AggregateAlternative;

            AssertFetchXml(new[] { alterativeQuery }, @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <attribute name='accountid' />
                        <order attribute='name' />
                    </entity>
                </fetch>
            ");

            CollectionAssert.AreEqual(simpleAggregate.ColumnSet, alterativeQuery.ColumnSet);

            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var guid3 = Guid.NewGuid();

            context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [guid1] = new Entity("account", guid1)
                {
                    ["name"] = "Data8",
                    ["accountid"] = guid1
                },
                [guid2] = new Entity("account", guid2)
                {
                    ["name"] = "Data8",
                    ["accountid"] = guid2
                },
                [guid3] = new Entity("account", guid3)
                {
                    ["name"] = "Microsoft",
                    ["accountid"] = guid3
                }
            };

            alterativeQuery.Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)alterativeQuery.Result;
            Assert.AreEqual(2, results.Entities.Count);

            Assert.AreEqual("Data8", results.Entities[0].GetAttributeValue<string>("name"));
            Assert.AreEqual(2, results.Entities[0].GetAttributeValue<int>(simpleAggregate.ColumnSet[1]));
        }

        [TestMethod]
        public void GuidEntityReferenceInequality()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT a.name FROM account a INNER JOIN contact c ON a.primarycontactid = c.contactid WHERE (c.parentcustomerid is null or a.accountid <> c.parentcustomerid)";

            var queries = sql2FetchXml.Convert(query);

            var select = (SelectQuery)queries[0];

            var account1 = Guid.NewGuid();
            var account2 = Guid.NewGuid();
            var contact1 = Guid.NewGuid();
            var contact2 = Guid.NewGuid();

            context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [account1] = new Entity("account", account1)
                {
                    ["name"] = "Data8",
                    ["accountid"] = account1,
                    ["primarycontactid"] = new EntityReference("contact", contact1)
                },
                [account2] = new Entity("account", account2)
                {
                    ["name"] = "Microsoft",
                    ["accountid"] = account2,
                    ["primarycontactid"] = new EntityReference("contact", contact2)
                }
            };
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [contact1] = new Entity("contact", contact1)
                {
                    ["parentcustomerid"] = new EntityReference("account", account2),
                    ["contactid"] = contact1
                },
                [contact2] = new Entity("contact", contact2)
                {
                    ["parentcustomerid"] = new EntityReference("account", account2),
                    ["contactid"] = contact2
                }
            };

            select.Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);
            var results = (EntityCollection)select.Result;
            Assert.AreEqual(1, results.Entities.Count);

            Assert.AreEqual("Data8", results.Entities[0].GetAttributeValue<string>("name"));
        }

        [TestMethod]
        public void UpdateGuidToEntityReference()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "UPDATE a SET primarycontactid = c.contactid FROM account AS a INNER JOIN contact AS c ON a.accountid = c.parentcustomerid";

            var queries = sql2FetchXml.Convert(query);

            var update = (UpdateQuery)queries[0];

            var account1 = Guid.NewGuid();
            var account2 = Guid.NewGuid();
            var contact1 = Guid.NewGuid();
            var contact2 = Guid.NewGuid();

            context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [account1] = new Entity("account", account1)
                {
                    ["name"] = "Data8",
                    ["accountid"] = account1
                },
                [account2] = new Entity("account", account2)
                {
                    ["name"] = "Microsoft",
                    ["accountid"] = account2
                }
            };
            context.Data["contact"] = new Dictionary<Guid, Entity>
            {
                [contact1] = new Entity("contact", contact1)
                {
                    ["parentcustomerid"] = new EntityReference("account", account1),
                    ["contactid"] = contact1
                },
                [contact2] = new Entity("contact", contact2)
                {
                    ["parentcustomerid"] = new EntityReference("account", account2),
                    ["contactid"] = contact2
                }
            };

            update.Execute(context.GetOrganizationService(), new AttributeMetadataCache(context.GetOrganizationService()), this);

            Assert.AreEqual(new EntityReference("contact", contact1), context.Data["account"][account1].GetAttributeValue<EntityReference>("primarycontactid"));
            Assert.AreEqual(new EntityReference("contact", contact2), context.Data["account"][account2].GetAttributeValue<EntityReference>("primarycontactid"));
        }

        [TestMethod]
        public void CompareDateFields()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "DELETE c2 FROM contact c1 INNER JOIN contact c2 ON c1.parentcustomerid = c2.parentcustomerid AND c2.createdon > c1.createdon";

            var ex = Assert.ThrowsException<NotSupportedQueryFragmentException>(() => sql2FetchXml.Convert(query));
            Assert.IsTrue(ex.Message.Contains("rewrite as WHERE clause"));
        }

        [TestMethod]
        public void ColumnComparison()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);
            sql2FetchXml.ColumnComparisonAvailable = true;

            var query = "SELECT firstname, lastname FROM contact WHERE firstname = lastname";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                        <filter>
                            <condition attribute='firstname' operator='eq' valueof='lastname' />
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void QuotedIdentifierError()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, lastname FROM contact WHERE firstname = \"mark\"";

            try
            {
                sql2FetchXml.Convert(query);
                Assert.Fail("Expected exception");
            }
            catch (NotSupportedQueryFragmentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Did you mean 'mark'?"));
            }
        }

        [TestMethod]
        public void FilterExpressionConstantValueToFetchXml()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT firstname, lastname FROM contact WHERE firstname = 'Ma' + 'rk'";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, $@"
                <fetch>
                    <entity name='contact'>
                        <attribute name='firstname' />
                        <attribute name='lastname' />
                        <filter>
                            <condition attribute='firstname' operator='eq' value='Mark' />
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void Count1ConvertedToCountStar()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT COUNT(1) FROM contact";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, $@"
                <fetch aggregate='true'>
                    <entity name='contact'>
                        <attribute name='contactid' aggregate='count' alias='contactid_count' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void CaseInsensitive()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "Select Name From Account";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, $@"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void ContainsValues()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name FROM account WHERE CONTAINS(name, '1 OR 2')";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, $@"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='contain-values'>
                                <value>1</value>
                                <value>2</value>
                            </condition>
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void NotContainsValues()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT name FROM account WHERE NOT CONTAINS(name, '1 OR 2')";

            var queries = sql2FetchXml.Convert(query);

            AssertFetchXml(queries, $@"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='not-contain-values'>
                                <value>1</value>
                                <value>2</value>
                            </condition>
                        </filter>
                    </entity>
                </fetch>
            ");
        }

        [TestMethod]
        public void GlobalOptionSet()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());
            context.AddFakeMessageExecutor<RetrieveAllOptionSetsRequest>(new RetrieveAllOptionSetsHandler());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT displayname FROM globaloptionset WHERE name = 'test'";

            var queries = sql2FetchXml.Convert(query);

            Assert.IsInstanceOfType(queries.Single(), typeof(GlobalOptionSetQuery));

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='globaloptionset'>
                        <attribute name='displayname' />
                        <filter>
                            <condition attribute='name' operator='eq' value='test' />
                        </filter>
                    </entity>
                </fetch>");

            queries[0].Execute(org, metadata, this);

            var result = (DataTable)queries[0].Result;

            Assert.AreEqual(1, result.Rows.Count);
        }

        [TestMethod]
        public void GlobalOptionSetTranslations()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());
            context.AddFakeMessageExecutor<RetrieveAllOptionSetsRequest>(new RetrieveAllOptionSetsHandler());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);

            var query = "SELECT gos.displayname, dn.label FROM globaloptionset AS gos INNER JOIN localizedlabel AS dn ON gos.displaynameid = dn.labelid WHERE gos.name = 'test'";

            var queries = sql2FetchXml.Convert(query);

            Assert.IsInstanceOfType(queries.Single(), typeof(GlobalOptionSetQuery));

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='globaloptionset'>
                        <attribute name='displayname' />
                        <link-entity name='localizedlabel' from='labelid' to='displaynameid' link-type='inner' alias='dn'>
                            <attribute name='label' />
                        </link-entity>
                        <filter>
                            <condition attribute='name' operator='eq' value='test' />
                        </filter>
                    </entity>
                </fetch>");

            queries[0].Execute(org, metadata, this);

            var result = (DataTable)queries[0].Result;

            Assert.AreEqual(2, result.Rows.Count);
        }

        [TestMethod]
        public void EntityDetails()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);
            context.AddFakeMessageExecutor<RetrieveMetadataChangesRequest>(new RetrieveMetadataChangesHandler(metadata));

            var query = "SELECT logicalname FROM entity ORDER BY 1";

            var queries = sql2FetchXml.Convert(query);

            Assert.IsInstanceOfType(queries.Single(), typeof(EntityMetadataQuery));

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='entity'>
                        <attribute name='logicalname' />
                        <order attribute='logicalname' />
                    </entity>
                </fetch>");

            queries[0].Execute(org, metadata, this);

            var result = (DataTable)queries[0].Result;

            Assert.AreEqual(3, result.Rows.Count);
            Assert.AreEqual("account", result.Rows[0]["logicalname"]);
            Assert.AreEqual("contact", result.Rows[1]["logicalname"]);
            Assert.AreEqual("new_customentity", result.Rows[2]["logicalname"]);
        }

        [TestMethod]
        public void AttributeDetails()
        {
            var context = new XrmFakedContext();
            context.InitializeMetadata(Assembly.GetExecutingAssembly());

            var org = context.GetOrganizationService();
            var metadata = new AttributeMetadataCache(org);
            var sql2FetchXml = new Sql2FetchXml(metadata, true);
            context.AddFakeMessageExecutor<RetrieveMetadataChangesRequest>(new RetrieveMetadataChangesHandler(metadata));

            var query = "SELECT e.logicalname, a.logicalname FROM entity e INNER JOIN attribute a ON e.logicalname = a.entitylogicalname WHERE e.logicalname = 'new_customentity' ORDER BY 1, 2";

            var queries = sql2FetchXml.Convert(query);

            Assert.IsInstanceOfType(queries.Single(), typeof(EntityMetadataQuery));

            AssertFetchXml(queries, @"
                <fetch>
                    <entity name='entity'>
                        <attribute name='logicalname' />
                        <link-entity name='attribute' from='entitylogicalname' to='logicalname' alias='a' link-type='inner'>
                            <attribute name='logicalname' />
                            <order attribute='logicalname' />
                        </link-entity>
                        <filter type='and'>
                            <condition attribute='logicalname' operator='eq' value='new_customentity' />
                        </filter>
                        <order attribute='logicalname' />
                    </entity>
                </fetch>");

            queries[0].Execute(org, metadata, this);

            var result = (DataTable)queries[0].Result;

            Assert.AreEqual(3, result.Rows.Count);
            Assert.AreEqual("new_customentityid", result.Rows[0]["a.logicalname"]);
            Assert.AreEqual("new_name", result.Rows[1]["a.logicalname"]);
            Assert.AreEqual("new_parentid", result.Rows[2]["a.logicalname"]);
        }

        private void AssertFetchXml(Query[] queries, string fetchXml)
        {
            Assert.AreEqual(1, queries.Length);
            Assert.IsInstanceOfType(queries[0], typeof(FetchXmlQuery));

            var serializer = new XmlSerializer(typeof(FetchXml.FetchType));
            using (var reader = new StringReader(fetchXml))
            {
                var fetch = (FetchXml.FetchType)serializer.Deserialize(reader);
                PropertyEqualityAssert.Equals(fetch, ((FetchXmlQuery)queries[0]).FetchXml);
            }
        }

        void IQueryExecutionOptions.Progress(string message)
        {
        }

        bool IQueryExecutionOptions.ContinueRetrieve(int count)
        {
            return true;
        }

        bool IQueryExecutionOptions.ConfirmUpdate(int count, EntityMetadata meta)
        {
            return true;
        }

        bool IQueryExecutionOptions.ConfirmDelete(int count, EntityMetadata meta)
        {
            return true;
        }

        private class RetrieveMetadataChangesHandler : IFakeMessageExecutor
        {
            private readonly IAttributeMetadataCache _metadata;

            public RetrieveMetadataChangesHandler(IAttributeMetadataCache metadata)
            {
                _metadata = metadata;
            }

            public bool CanExecute(OrganizationRequest request)
            {
                return request is RetrieveMetadataChangesRequest;
            }

            public OrganizationResponse Execute(OrganizationRequest request, XrmFakedContext ctx)
            {
                var metadata = new EntityMetadataCollection
                {
                    _metadata["account"],
                    _metadata["contact"],
                    _metadata["new_customentity"]
                };

                foreach (var entity in metadata)
                {
                    if (entity.MetadataId == null)
                        entity.MetadataId = Guid.NewGuid();

                    foreach (var attribute in entity.Attributes)
                    {
                        if (attribute.MetadataId == null)
                            attribute.MetadataId = Guid.NewGuid();
                    }
                }

                var response = new RetrieveMetadataChangesResponse
                {
                    Results = new ParameterCollection
                    {
                        ["EntityMetadata"] = metadata
                    }
                };

                return response;
            }

            public Type GetResponsibleRequestType()
            {
                return typeof(RetrieveMetadataChangesRequest);
            }
        }

        private class RetrieveAllOptionSetsHandler : IFakeMessageExecutor
        {
            public bool CanExecute(OrganizationRequest request)
            {
                return request is RetrieveAllOptionSetsRequest;
            }

            public OrganizationResponse Execute(OrganizationRequest request, XrmFakedContext ctx)
            {
                var labels = new[]
                {
                    new LocalizedLabel("TestGlobalOptionSet", 1033) { MetadataId = Guid.NewGuid() },
                    new LocalizedLabel("TranslatedDisplayName-Test", 9999) { MetadataId = Guid.NewGuid() },
                    new LocalizedLabel("FooGlobalOptionSet", 1033) { MetadataId = Guid.NewGuid() },
                    new LocalizedLabel("TranslatedDisplayName-Foo", 9999) { MetadataId = Guid.NewGuid() },
                    new LocalizedLabel("BarGlobalOptionSet", 1033) { MetadataId = Guid.NewGuid() },
                    new LocalizedLabel("TranslatedDisplayName-Bar", 9999) { MetadataId = Guid.NewGuid() }
                };

                return new RetrieveAllOptionSetsResponse
                {
                    Results = new ParameterCollection
                    {
                        ["OptionSetMetadata"] = new OptionSetMetadataBase[]
                        {
                            new OptionSetMetadata(new OptionMetadataCollection(new[]
                            {
                                new OptionMetadata(new Label("Value1", 1033), 1),
                                new OptionMetadata(new Label("Value2", 1033), 2)
                            }))
                            {
                                MetadataId = Guid.NewGuid(),
                                Name = "test",
                                DisplayName = new Label(
                                    labels[0],
                                    new[]
                                    {
                                        labels[0],
                                        labels[1]
                                    })
                            },
                            new OptionSetMetadata(new OptionMetadataCollection(new[]
                            {
                                new OptionMetadata(new Label("Value1", 1033), 1),
                                new OptionMetadata(new Label("Value2", 1033), 2)
                            }))
                            {
                                MetadataId = Guid.NewGuid(),
                                Name = "foo",
                                DisplayName = new Label(
                                    labels[2],
                                    new[]
                                    {
                                        labels[2],
                                        labels[3]
                                    })
                            },
                            new OptionSetMetadata(new OptionMetadataCollection(new[]
                            {
                                new OptionMetadata(new Label("Value1", 1033), 1),
                                new OptionMetadata(new Label("Value2", 1033), 2)
                            }))
                            {
                                MetadataId = Guid.NewGuid(),
                                Name = "bar",
                                DisplayName = new Label(
                                    labels[4],
                                    new[]
                                    {
                                        labels[4],
                                        labels[5]
                                    })
                            }
                        }
                    }
                };
            }

            public Type GetResponsibleRequestType()
            {
                return typeof(RetrieveAllOptionSetsRequest);
            }
        }
    }
}
