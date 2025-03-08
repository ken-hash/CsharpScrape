using AutoFixture.Kernel;

namespace localscrapeTests.Helpers
{
    class NoNumberCustomisation : ISpecimenBuilder
    {
        private static readonly Random _random = new Random();
        private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public object Create(object request, ISpecimenContext context)
        {
            if (request is Type type && type == typeof(string))
            {
                int length = _random.Next(5, 15); // Random string length
                return new string(Enumerable.Repeat(Letters, length)
                    .Select(s => s[_random.Next(s.Length)]).ToArray());
            }
            return new NoSpecimen();
        }
    }
}
