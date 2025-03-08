using AutoFixture;
using localscrapeTests.Helpers;

namespace localscrapeTests
{
    public class NaturalSortTest
    {
        private Fixture _fixture;
        [SetUp]
        public void Setup()
        {
            _fixture = new Fixture();
            _fixture.Customizations.Add(new NoNumberCustomisation());
        }

        [TestCase("12", "3", "4", ExpectedResult = "12")]
        [TestCase("Chapter 12", "Chapter 3", "Chapter 4", ExpectedResult = "Chapter 12")]
        [TestCase("Chapter 12", "Chapter 22", "Chapter 45", ExpectedResult = "Chapter 45")]
        public string ReturnHighestNumber(string a, string b, string c)
        {
            var result = new List<string> { a, b, c }
                .OrderBy(x => x, new NaturalSortComparer()).Last();
            return result;
        }

        [TestCase("12", "3", "4", ExpectedResult = "3")]
        [TestCase("Chapter 12", "Chapter 3", "Chapter 4", ExpectedResult = "Chapter 3")]
        [TestCase("Chapter 12", "Chapter 22", "Chapter 45", ExpectedResult = "Chapter 12")]
        public string ReturnLowestNumber(string a, string b, string c)
        {
            var result = new List<string> { a, b, c }
                .OrderBy(x => x, new NaturalSortComparer()).First();
            return result;
        }

        [Test]
        public void NoNumbersStringComesFirst()
        {
            var nonNumber = _fixture.Create<string>();
            var number = _fixture.Create<int>().ToString();
            var result = new List<string> { nonNumber, number }
                .OrderBy(x => x, new NaturalSortComparer()).First();
            Assert.That(result, Is.EqualTo(nonNumber));
        }

        [Test]
        public void NumberSuffixComesLast()
        {
            var number = _fixture.Create<int>();
            var prefixNumber = $"{number}{_fixture.Create<string>()}";
            var suffixNumber = $"{_fixture.Create<string>()}{number}";
            var result = new List<string> { prefixNumber, suffixNumber }
                .OrderBy(x => x, new NaturalSortComparer()).Last();
            Assert.That(result, Is.EqualTo(suffixNumber));
        }

        [Test]
        public void WhichEverNumberIsGreaterRegardlessOfPosition()
        {
            var number = _fixture.Create<int>();
            var prefixNumber = $"{number}{_fixture.Create<string>()}";
            var suffixNumber = $"{_fixture.Create<string>()}{number+1}";
            var result = new List<string> { prefixNumber, suffixNumber }
                .OrderBy(x => x, new NaturalSortComparer()).Last();
            Assert.That(result, Is.EqualTo(suffixNumber));
        }
    }
}