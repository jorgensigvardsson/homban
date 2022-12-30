using AutoFixture;
using AutoFixture.AutoMoq;
using Moq;

namespace Sigvardsson.Homban.Api.UnitTests.Infrastructure;

public abstract class TestBase
{
    protected readonly Fixture m_fixture;

    protected TestBase()
    {
        m_fixture = new Fixture();
    }

    protected T CreateSpecimen<T>()
    {
        return m_fixture.Create<T>();
    }

    protected Mock<T> CreateMock<T>() where T : class => new Mock<T>(MockBehavior.Loose);
}

public abstract class TestBase<TSut> : TestBase
{
    protected readonly Fixture m_sutFixture;

    protected TestBase()
    {
        m_sutFixture = new Fixture { OmitAutoProperties = true };
        m_sutFixture.Customize(new AutoMoqCustomization { ConfigureMembers = false });
    }
    
    protected Mock<TDep> InjectMock<TDep>() where TDep : class
    {
        return m_sutFixture.Freeze<Mock<TDep>>();
    }

    protected TDep InjectValue<TDep>(TDep value)
    {
        m_sutFixture.Inject(value);
        return value;
    }
    
    protected TSut CreateSut()
    {
        return m_sutFixture.Create<TSut>();
    }
}