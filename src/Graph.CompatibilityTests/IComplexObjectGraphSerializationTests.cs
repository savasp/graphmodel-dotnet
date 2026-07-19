// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

// Every test in this interface persists, updates, deletes, or navigates a complex-property subtree
// (nested owned objects stored as part of their owning node), so the whole area is gated on the
// ComplexPropertyCascade capability - a provider that does not declare it skips all of them.
[RequiresCapability(GraphCapability.ComplexPropertyCascade)]
public interface IComplexObjectGraphSerializationTests : IGraphTest
{
    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperty()
    {
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexPropertyTree()
    {
        // Create n1 -> A -> B
        var nestedA = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" };
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = nestedA };
        nestedA.B = new ComplexClassB { Property1 = "Nested B1" };

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
        Assert.Equal(n1.A?.B?.Property1, fetched.A?.B?.Property1);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexGraph()
    {
        // Create
        // n1 -> A
        var nestedA = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" };
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = nestedA };
        // A -> B
        nestedA.B = new ComplexClassB { Property1 = "Nested B1" };
        // A -> C
        nestedA.C = new ComplexClassC { Property1 = "Nested C1" };
        // C -> B
        nestedA.C.B = new ComplexClassB();

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
        Assert.Equal(n1.A?.B?.Property1, fetched.A?.B?.Property1);
        Assert.Equal(n1.A?.C?.Property1, fetched.A?.C?.Property1);
        Assert.NotNull(fetched.A?.C?.B);
        Assert.Equal(n1.A?.C?.B?.Property1, fetched.A?.C?.B?.Property1);
    }

    [Fact]
    public async Task CannotCreateNodeWithObjectGraphWithCycles()
    {
        // Create n1 -> A -> B -> A
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };
        n1.A.B = new ComplexClassB { Property1 = "Nested B1" };
        n1.A.B.A = n1.A; // Cycle

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithListOfComplexProperties()
    {
        // Create
        var n1 = new Class2 { Property1 = "Value A", Property2 = "Value B" };
        n1.A.Add(new ComplexClassA { Property1 = "Nested A1", Property2 = "Nested B1" });
        n1.A.Add(new ComplexClassA { Property1 = "Nested A2", Property2 = "Nested B2" });
        n1.B.Add(new ComplexClassB { Property1 = "Nested B1" });
        n1.B.Add(new ComplexClassB { Property1 = "Nested B2" });
        n1.A[0].B = new ComplexClassB { Property1 = "Nested B3" };
        n1.A[0].C = new ComplexClassC { Property1 = "Nested C1" };
        n1.A[1].B = n1.A[0].B; // Share B between A[0] and A[1]
        n1.B[0].A = n1.A[0]; // Share A[0] with B[0]

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class2>(n1.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A.Count, fetched.A.Count);
        Assert.Equal(n1.B.Count, fetched.B.Count);
        Assert.Equal(n1.A[0].Property1, fetched.A[0].Property1);
        Assert.Equal(n1.A[0].Property2, fetched.A[0].Property2);
        Assert.Equal(n1.A[0].B?.Property1, fetched.A[0].B?.Property1);
        Assert.Equal(n1.B[0].Property1, fetched.B[0].Property1);
        Assert.Equal(n1.B[1].Property1, fetched.B[1].Property1);
        Assert.Equal(n1.A[1].Property1, fetched.A[1].Property1);
        Assert.Equal(n1.A[1].Property2, fetched.A[1].Property2);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithMixedDerivedInstancesInBaseTypedComplexPropertyCollection()
    {
        // 3-level hierarchy (AnimalDescription <- DogDescription <- PoliceDogDescription), mixed order,
        // and a nested complex property (Handler) only present on the most-derived level - all three
        // must survive the round trip through a `List<AnimalDescription>` complex-property collection (#146).
        var kennel = new Kennel
        {
            Name = "Central Kennel",
            Animals =
            [
                new DogDescription { Name = "Rex", Breed = "Labrador" },
                new PoliceDogDescription
                {
                    Name = "K9",
                    Breed = "Shepherd",
                    Badge = "K9-42",
                    Handler = new HandlerDescription { Name = "Officer Diaz" },
                },
                new AnimalDescription { Name = "Generic Animal" },
                new DogDescription { Name = "Fido", Breed = "Poodle" },
            ],
        };

        await this.Graph.CreateNodeAsync(kennel, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Kennel>(kennel.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(kennel.Animals.Count, fetched.Animals.Count);

        // Order preserved.
        for (var i = 0; i < kennel.Animals.Count; i++)
        {
            Assert.Equal(kennel.Animals[i].GetType(), fetched.Animals[i].GetType());
            Assert.Equal(kennel.Animals[i].Name, fetched.Animals[i].Name);
        }

        // Types preserved, including the mixed derived instances at every level of the hierarchy.
        Assert.IsType<DogDescription>(fetched.Animals[0]);
        var policeDog = Assert.IsType<PoliceDogDescription>(fetched.Animals[1]);
        Assert.Equal("K9-42", policeDog.Badge);

        // Nested complex property on the derived element is intact.
        Assert.NotNull(policeDog.Handler);
        Assert.Equal("Officer Diaz", policeDog.Handler!.Name);

        Assert.IsType<AnimalDescription>(fetched.Animals[2]);
        Assert.IsType<DogDescription>(fetched.Animals[3]);
    }

    [Fact]
    [RequiresCapability(GraphCapability.Transactions)]
    public async Task ConcurrentUpdates_SeparateTransactions_OneValueNodeNoOrphans()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var original = new ContractAddressValue { City = "Seattle", Street = "Concurrent Original" };
        var owner = new ContractAddressOwner { Address = original };
        await Graph.CreateNodeAsync(owner, null, cancellationToken);

        var payloadA = new ContractAddressValue { City = "Denver", Street = "Concurrent Writer A" };
        var payloadB = new ContractAddressValue { City = "Austin", Street = "Concurrent Writer B" };
        var updateBarrier = new AsyncBarrier(participantCount: 2);

        var failures = await Task.WhenAll(
            UpdateInOwnTransactionAsync(owner with { Address = payloadA }, updateBarrier, cancellationToken),
            UpdateInOwnTransactionAsync(owner with { Address = payloadB }, updateBarrier, cancellationToken));

        AssertExpectedConcurrentFailures(failures);
        Assert.True(
            failures.Any(failure => failure is null),
            $"Both concurrent updates failed: {failures[0]}; {failures[1]}");

        var fetched = await Graph.GetNodeAsync<ContractAddressOwner>(owner.Id, null, cancellationToken);
        var successfulPayloads = new List<ContractAddressValue>();
        if (failures[0] is null) successfulPayloads.Add(payloadA);
        if (failures[1] is null) successfulPayloads.Add(payloadB);
        Assert.Contains(fetched.Address, successfulPayloads);
        Assert.Equal(1, await CountContractAddressValuesAsync(
            [original.Street, payloadA.Street, payloadB.Street], cancellationToken));
    }

    [Fact]
    [RequiresCapability(GraphCapability.Transactions)]
    public async Task ConcurrentCollectionUpdates_SeparateTransactions_OneWriterItemsNoOrphans()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var original = new List<ContractAddressValue>
        {
            new() { City = "Seattle", Street = "Collection Original 1" },
            new() { City = "Tacoma", Street = "Collection Original 2" },
        };
        var owner = new ContractAddressCollectionOwner { Addresses = original };
        await Graph.CreateNodeAsync(owner, null, cancellationToken);

        var payloadA = new List<ContractAddressValue>
        {
            new() { City = "Denver", Street = "Collection Writer A1" },
            new() { City = "Boulder", Street = "Collection Writer A2" },
            new() { City = "Golden", Street = "Collection Writer A3" },
        };
        var payloadB = new List<ContractAddressValue>
        {
            new() { City = "Austin", Street = "Collection Writer B1" },
        };
        var updateBarrier = new AsyncBarrier(participantCount: 2);

        var failures = await Task.WhenAll(
            UpdateInOwnTransactionAsync(owner with { Addresses = payloadA }, updateBarrier, cancellationToken),
            UpdateInOwnTransactionAsync(owner with { Addresses = payloadB }, updateBarrier, cancellationToken));

        AssertExpectedConcurrentFailures(failures);
        Assert.True(
            failures.Any(failure => failure is null),
            $"Both concurrent updates failed: {failures[0]}; {failures[1]}");

        var fetched = await Graph.GetNodeAsync<ContractAddressCollectionOwner>(owner.Id, null, cancellationToken);
        var successfulPayloads = new List<List<ContractAddressValue>>();
        if (failures[0] is null) successfulPayloads.Add(payloadA);
        if (failures[1] is null) successfulPayloads.Add(payloadB);
        Assert.True(
            successfulPayloads.Any(payload => payload.SequenceEqual(fetched.Addresses)),
            "The surviving collection must be one complete successful-writer payload.");

        var candidateStreets = original.Concat(payloadA).Concat(payloadB).Select(address => address.Street).ToArray();
        Assert.Equal(
            fetched.Addresses.Count,
            await CountContractAddressValuesAsync(candidateStreets, cancellationToken));
    }

    [Fact]
    public async Task UpdateCleanup_RemovesOnlyComplexValue_DomainRelationshipAndNodeSurvive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (owner, domainNode, domainRelationship) = await SeedCollisionAsync(
            complexCity: "Complex Seattle",
            domainCity: "Domain Portland",
            cancellationToken);

        var originalStreet = owner.Address.Street;
        var replacement = new ContractAddressValue { City = "Complex Denver", Street = "16th St" };
        await Graph.UpdateNodeAsync(owner with { Address = replacement }, null, cancellationToken);

        var survivingNode = await Graph.GetNodeAsync<ContractAddressNode>(domainNode.Id, null, cancellationToken);
        Assert.Equal(domainNode.Id, survivingNode.Id);
        Assert.Equal(domainNode.Street, survivingNode.Street);
        Assert.Equal(domainNode.City, survivingNode.City);
        var survivingRelationship = await Graph.GetRelationshipAsync<ContractPrimaryAddress>(
            domainRelationship.Id,
            null,
            cancellationToken);
        Assert.Equal(domainRelationship.Id, survivingRelationship.Id);
        Assert.Equal(domainRelationship.StartNodeId, survivingRelationship.StartNodeId);
        Assert.Equal(domainRelationship.EndNodeId, survivingRelationship.EndNodeId);

        var fetched = await Graph.GetNodeAsync<ContractAddressOwner>(owner.Id, null, cancellationToken);
        Assert.Equal(replacement, fetched.Address);
        Assert.Equal(1, await CountContractAddressValuesAsync(
            [originalStreet, replacement.Street], cancellationToken));
    }

    [Fact]
    public async Task CascadeDeleteCleanup_RemovesComplexValue_DomainNodeSurvives()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (owner, domainNode, _) = await SeedCollisionAsync(
            complexCity: "Complex Vancouver",
            domainCity: "Domain Tacoma",
            cancellationToken);

        await Graph.DeleteNodeAsync(owner.Id, cascadeDelete: true, null, cancellationToken);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Graph.GetNodeAsync<ContractAddressOwner>(owner.Id, null, cancellationToken));
        Assert.Equal(
            0,
            await CountContractAddressValuesAsync([owner.Address.Street], cancellationToken));
        var survivingNode = await Graph.GetNodeAsync<ContractAddressNode>(domainNode.Id, null, cancellationToken);
        Assert.Equal(domainNode.Id, survivingNode.Id);
        Assert.Equal(domainNode.Street, survivingNode.Street);
        Assert.Equal(domainNode.City, survivingNode.City);
    }

    [Fact]
    public async Task Navigation_ComplexPropertyPredicate_MatchesCollidingDomainRelationship()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (owner, _, _) = await SeedCollisionAsync(
            complexCity: "Complex Austin",
            domainCity: "Domain Boulder",
            cancellationToken);

        var viaDomainValue = await Graph.Nodes<ContractAddressOwner>()
            .Where(candidate => candidate.Address.City == "Domain Boulder")
            .ToListAsync(cancellationToken);
        Assert.Contains(viaDomainValue, candidate => candidate.Id == owner.Id);

        var viaComplexValue = await Graph.Nodes<ContractAddressOwner>()
            .Where(candidate => candidate.Address.City == "Complex Austin")
            .ToListAsync(cancellationToken);
        var fetched = Assert.Single(viaComplexValue, candidate => candidate.Id == owner.Id);
        Assert.Equal("Complex Austin", fetched.Address.City);

        var splitAcrossTargets = await Graph.Nodes<ContractAddressOwner>()
            .Where(candidate => candidate.Address.City == "Domain Boulder")
            .Where(candidate => candidate.Address.Street == "1st Ave")
            .ToListAsync(cancellationToken);
        Assert.DoesNotContain(splitAcrossTargets, candidate => candidate.Id == owner.Id);
    }

    private async Task<int> CountContractAddressValuesAsync(
        IReadOnlyCollection<string> streets,
        CancellationToken cancellationToken) =>
        await Harness.CountNodesByPropertyAsync(
            Graph,
            label: nameof(ContractAddressValue),
            propertyName: nameof(ContractAddressValue.Street),
            streets,
            cancellationToken);

    private async Task<(
        ContractAddressOwner Owner,
        ContractAddressNode DomainNode,
        ContractPrimaryAddress DomainRelationship)> SeedCollisionAsync(
        string complexCity,
        string domainCity,
        CancellationToken cancellationToken)
    {
        var owner = new ContractAddressOwner
        {
            Address = new ContractAddressValue { City = complexCity, Street = "1st Ave" },
        };
        await Graph.CreateNodeAsync(owner, null, cancellationToken);

        var domainNode = new ContractAddressNode { City = domainCity, Street = "2nd Ave" };
        await Graph.CreateNodeAsync(domainNode, null, cancellationToken);

        var domainRelationship = new ContractPrimaryAddress(owner.Id, domainNode.Id);
        await Graph.CreateRelationshipAsync(domainRelationship, null, cancellationToken);
        return (owner, domainNode, domainRelationship);
    }

    private async Task<Exception?> UpdateInOwnTransactionAsync<TNode>(
        TNode node,
        AsyncBarrier updateBarrier,
        CancellationToken cancellationToken)
        where TNode : class, INode
    {
        var barrierSignaled = false;
        try
        {
            await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
            barrierSignaled = true;
            await updateBarrier.SignalAndWaitAsync(cancellationToken);
            await Graph.UpdateNodeAsync(node, transaction, cancellationToken);
            await transaction.CommitAsync();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!barrierSignaled)
            {
                updateBarrier.Signal();
            }

            return exception;
        }
    }

    private void AssertExpectedConcurrentFailures(IEnumerable<Exception?> failures)
    {
        foreach (var failure in failures.OfType<Exception>())
        {
            Assert.True(
                Harness.IsExpectedConcurrentUpdateException(failure),
                $"Unexpected concurrent update failure: {failure}");
        }
    }

}
