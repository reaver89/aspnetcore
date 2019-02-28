// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Internal;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.TestObjects;
using Microsoft.AspNetCore.Routing.Tree;
using Xunit;

namespace Microsoft.AspNetCore.Routing
{
    public class RouteValuesAddressSchemeTest
    {
        [Fact]
        public void GetOutboundMatches_GetsRouteNameMatchesFor_EndpointsHaving_IRouteNameMetadata()
        {
            // Arrange
            var endpoint1 = CreateEndpoint("/a", routeName: "other");
            var endpoint2 = CreateEndpoint("/a", routeName: "named");

            // Act
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2);

            // Assert
            Assert.NotNull(addressScheme.State.Endpoints);
            Assert.Equal(2, addressScheme.State.Endpoints.Count());
            Assert.NotNull(addressScheme.State.RouteNameMatches);
            Assert.True(addressScheme.State.RouteNameMatches.TryGetValue("named", out var RouteNameMatches));
            var namedMatch = Assert.Single(RouteNameMatches);
            var actual = Assert.IsType<RouteEndpoint>(namedMatch);
            Assert.Same(endpoint2, actual);
        }

        [Fact]
        public void GetOutboundMatches_GroupsMultipleEndpoints_WithSameName()
        {
            // Arrange
            var endpoint1 = CreateEndpoint("/a", routeName: "other");
            var endpoint2 = CreateEndpoint("/a", routeName: "named");
            var endpoint3 = CreateEndpoint("/b", routeName: "named");

            // Act
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2, endpoint3);

            // Assert
            Assert.NotNull(addressScheme.State.Endpoints);
            Assert.Equal(3, addressScheme.State.Endpoints.Count());
            Assert.NotNull(addressScheme.State.RouteNameMatches);
            Assert.True(addressScheme.State.RouteNameMatches.TryGetValue("named", out var RouteNameMatches));
            Assert.Equal(2, RouteNameMatches.Count);
            Assert.Same(endpoint2, Assert.IsType<RouteEndpoint>(RouteNameMatches[0]));
            Assert.Same(endpoint3, Assert.IsType<RouteEndpoint>(RouteNameMatches[1]));
        }

        [Fact]
        public void GetOutboundMatches_GroupsMultipleEndpoints_WithSameName_IgnoringCase()
        {
            // Arrange
            var endpoint1 = CreateEndpoint("/a", routeName: "other");
            var endpoint2 = CreateEndpoint("/a", routeName: "named");
            var endpoint3 = CreateEndpoint("/b", routeName: "NaMed");

            // Act
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2, endpoint3);

            // Assert
            Assert.NotNull(addressScheme.State.Endpoints);
            Assert.Equal(3, addressScheme.State.Endpoints.Count());
            Assert.NotNull(addressScheme.State.RouteNameMatches);
            Assert.True(addressScheme.State.RouteNameMatches.TryGetValue("named", out var RouteNameMatches));
            Assert.Equal(2, RouteNameMatches.Count);
            Assert.Same(endpoint2, Assert.IsType<RouteEndpoint>(RouteNameMatches[0]));
            Assert.Same(endpoint3, Assert.IsType<RouteEndpoint>(RouteNameMatches[1]));
        }

        [Fact]
        public void EndpointDataSource_ChangeCallback_Refreshes_OutboundMatches()
        {
            // Arrange 1
            var endpoint1 = CreateEndpoint("/a", routeName: "a");
            var dynamicDataSource = new DynamicEndpointDataSource(new[] { endpoint1 });

            // Act 1
            var addressScheme = new RouteValuesAddressScheme(new CompositeEndpointDataSource(new[] { dynamicDataSource }));

            // Assert 1
            var state = addressScheme.State;
            Assert.NotNull(state.Endpoints);
            var match = Assert.Single(state.Endpoints);
            var actual = Assert.IsType<RouteEndpoint>(match);
            Assert.Same(endpoint1, actual);

            // Arrange 2
            var endpoint2 = CreateEndpoint("/b", routeName: "b");

            // Act 2
            // Trigger change
            dynamicDataSource.AddEndpoint(endpoint2);

            // Assert 2
            Assert.NotSame(state, addressScheme.State);
            state = addressScheme.State;

            // Arrange 3
            var endpoint3 = CreateEndpoint("/c", routeName: "c");

            // Act 3
            // Trigger change
            dynamicDataSource.AddEndpoint(endpoint3);

            // Assert 3
            Assert.NotSame(state, addressScheme.State);
            state = addressScheme.State;

            // Arrange 4
            var endpoint4 = CreateEndpoint("/d", routeName: "d");

            // Act 4
            // Trigger change
            dynamicDataSource.AddEndpoint(endpoint4);

            // Assert 4
            Assert.NotSame(state, addressScheme.State);
            state = addressScheme.State;

            Assert.NotNull(state.Endpoints);
            Assert.Collection(
                state.Endpoints,
                (m) =>
                {
                    actual = Assert.IsType<RouteEndpoint>(m);
                    Assert.Same(endpoint1, actual);
                },
                (m) =>
                {
                    actual = Assert.IsType<RouteEndpoint>(m);
                    Assert.Same(endpoint2, actual);
                },
                (m) =>
                {
                    actual = Assert.IsType<RouteEndpoint>(m);
                    Assert.Same(endpoint3, actual);
                },
                (m) =>
                {
                    actual = Assert.IsType<RouteEndpoint>(m);
                    Assert.Same(endpoint4, actual);
                });
        }

        [Fact]
        public void FindEndpoints_LookedUpByCriteria_NoMatch()
        {
            // Arrange
            var endpoint1 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { zipCode = 3510 },
                metadataRequiredValues: new { id = 7 });
            var endpoint2 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { id = 12 },
                metadataRequiredValues: new { zipCode = 3510 });
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 8 }),
                    AmbientValues = new RouteValueDictionary(new { urgent = false }),
                });

            // Assert
            Assert.Empty(foundEndpoints);
        }

        [Fact]
        public void FindEndpoints_LookedUpByCriteria_OneMatch()
        {
            // Arrange
            var endpoint1 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { zipCode = 3510 },
                metadataRequiredValues: new { id = 7 });
            var endpoint2 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { id = 12 });
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 7 }),
                    AmbientValues = new RouteValueDictionary(new { zipCode = 3500 }),
                });

            // Assert
            var actual = Assert.Single(foundEndpoints);
            Assert.Same(endpoint1, actual);
        }

        [Fact]
        public void FindEndpoints_LookedUpByCriteria_MultipleMatches()
        {
            // Arrange
            var endpoint1 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { zipCode = 3510 },
                metadataRequiredValues: new { id = 7 });
            var endpoint2 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent}/{zipCode}",
                defaults: new { id = 12 },
                metadataRequiredValues: new { id = 12 });
            var endpoint3 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { id = 12 },
                metadataRequiredValues: new { id = 12 });
            var addressScheme = CreateAddressScheme(endpoint1, endpoint2, endpoint3);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 12 }),
                    AmbientValues = new RouteValueDictionary(new { zipCode = 3500 }),
                });

            // Assert
            Assert.Collection(foundEndpoints,
                e => Assert.Equal(endpoint3, e),
                e => Assert.Equal(endpoint2, e));
        }

        [Fact]
        public void FindEndpoints_LookedUpByCriteria_ExcludeEndpointWithoutRouteValuesAddressMetadata()
        {
            // Arrange
            var endpoint1 = CreateEndpoint(
                "api/orders/{id}/{name?}/{urgent=true}/{zipCode}",
                defaults: new { zipCode = 3510 },
                metadataRequiredValues: new { id = 7 });
            var endpoint2 = CreateEndpoint("test");

            var addressScheme = CreateAddressScheme(endpoint1, endpoint2);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 7 }),
                    AmbientValues = new RouteValueDictionary(new { zipCode = 3500 }),
                }).ToList();

            // Assert
            Assert.DoesNotContain(endpoint2, foundEndpoints);
            Assert.Contains(endpoint1, foundEndpoints);
        }

        [Fact]
        public void FindEndpoints_ReturnsEndpoint_WhenLookedUpByRouteName()
        {
            // Arrange
            var expected = CreateEndpoint(
                "api/orders/{id}",
                defaults: new { controller = "Orders", action = "GetById" },
                metadataRequiredValues: new { controller = "Orders", action = "GetById" },
                routeName: "OrdersApi");
            var addressScheme = CreateAddressScheme(expected);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 10 }),
                    AmbientValues = new RouteValueDictionary(new { controller = "Home", action = "Index" }),
                    RouteName = "OrdersApi"
                });

            // Assert
            var actual = Assert.Single(foundEndpoints);
            Assert.Same(expected, actual);
        }

        [Fact]
        public void FindEndpoints_ReturnsEndpoint_UsingRoutePatternRequiredValues()
        {
            // Arrange
            var expected = CreateEndpoint(
                "api/orders/{id}",
                defaults: new { controller = "Orders", action = "GetById" },
                metadataRequiredValues: new { controller = "Orders", action = "GetById" });
            var addressScheme = CreateAddressScheme(expected);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(new { id = 10 }),
                    AmbientValues = new RouteValueDictionary(new { controller = "Orders", action = "GetById" }),
                });

            // Assert
            var actual = Assert.Single(foundEndpoints);
            Assert.Same(expected, actual);
        }

        [Fact]
        public void FindEndpoints_AlwaysReturnsEndpointsByRouteName_IgnoringMissingRequiredParameterValues()
        {
            // Here 'id' is the required value. The endpoint addressScheme would always return an endpoint by looking up
            // name only. Its the link generator which uses these endpoints finally to generate a link or not
            // based on the required parameter values being present or not.

            // Arrange
            var expected = CreateEndpoint(
                "api/orders/{id}",
                defaults: new { controller = "Orders", action = "GetById" },
                metadataRequiredValues: new { controller = "Orders", action = "GetById" },
                routeName: "OrdersApi");
            var addressScheme = CreateAddressScheme(expected);

            // Act
            var foundEndpoints = addressScheme.FindEndpoints(
                new RouteValuesAddress
                {
                    ExplicitValues = new RouteValueDictionary(),
                    AmbientValues = new RouteValueDictionary(),
                    RouteName = "OrdersApi"
                });

            // Assert
            var actual = Assert.Single(foundEndpoints);
            Assert.Same(expected, actual);
        }

        [Fact]
        public void GetOutboundMatches_DoesNotInclude_EndpointsWithSuppressLinkGenerationMetadata()
        {
            // Arrange
            var endpoint = CreateEndpoint(
                "/a",
                metadataCollection: new EndpointMetadataCollection(new[] { new SuppressLinkGenerationMetadata() }));

            // Act
            var addressScheme = CreateAddressScheme(endpoint);

            // Assert
            Assert.Empty(addressScheme.State.Endpoints);
        }

        [Fact]
        public void AddressScheme_UnsuppressedEndpoint_IsUsed()
        {
            // Arrange
            var endpoint = EndpointFactory.CreateRouteEndpoint(
                "/a",
                metadata: new object[] { new SuppressLinkGenerationMetadata(), new EncourageLinkGenerationMetadata(), new RouteNameMetadata(string.Empty), });

            // Act
            var addressScheme = CreateAddressScheme(endpoint);

            // Assert
            Assert.Same(endpoint, Assert.Single(addressScheme.State.Endpoints));
        }

        private RouteValuesAddressScheme CreateAddressScheme(params Endpoint[] endpoints)
        {
            return CreateAddressScheme(new DefaultEndpointDataSource(endpoints));
        }

        private RouteValuesAddressScheme CreateAddressScheme(params EndpointDataSource[] dataSources)
        {
            return new RouteValuesAddressScheme(new CompositeEndpointDataSource(dataSources));
        }

        private RouteEndpoint CreateEndpoint(
            string template,
            object defaults = null,
            object metadataRequiredValues = null,
            int order = 0,
            string routeName = null,
            EndpointMetadataCollection metadataCollection = null)
        {
            if (metadataCollection == null)
            {
                var metadata = new List<object>();
                if (!string.IsNullOrEmpty(routeName))
                {
                    metadata.Add(new RouteNameMetadata(routeName));
                }
                metadataCollection = new EndpointMetadataCollection(metadata);
            }

            return new RouteEndpoint(
                TestConstants.EmptyRequestDelegate,
                RoutePatternFactory.Parse(template, defaults, parameterPolicies: null, requiredValues: metadataRequiredValues),
                order,
                metadataCollection,
                null);
        }

        private class EncourageLinkGenerationMetadata : ISuppressLinkGenerationMetadata
        {
            public bool SuppressLinkGeneration => false;
        }
    }
}
