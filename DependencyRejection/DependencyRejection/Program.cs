using System;

namespace DependencyRejection
{
    class Program
    {
        static void Main(string[] args)
        {
            ReservationValidator validator = new ReservationValidator();
            ReservationsRepo repo = new ReservationsRepo();
            ReservationsService service = new ReservationsService();
            Logger logger = new Logger();

            ReservationsController m = new ReservationsController(validator,
                repo,
                service,
                logger);

            Console.WriteLine("Post result: " + m.Post(new ReservationRequestDto
            {
                Date = DateTime.Now,
                Email = "someemail@gmail.com",
                Name = "Eugene",
                Quantity = 1
            }));

            Console.ReadKey();
        }
    }
}
