using Monad;
using Monad.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Console;

namespace DependencyRejection
{
    public class ReservationsController
    {
        ReservationValidator _validator = new ReservationValidator();
        ReservationsRepo _repo = new ReservationsRepo();
        ReservationsService _service = new ReservationsService();
        Logger _logger = new Logger();

        // With classic DI you would inject the repos and logger to ReservationsService
        // but following Dependency Rejection we shouldn't mix side effects (Logger and ReservationsRepo)
        // with domain logic (ReservationsService).
        public ReservationsController(ReservationValidator validator,
            ReservationsRepo repo,
            ReservationsService service,
            Logger logger)
        {
            _validator = validator;
            _repo = repo;
            _service = service;
            _logger = logger;
        }

        public object Post(ReservationRequestDto dto)
        {
            const int CAPACITY = 100;

            // That's where the magic happens.
            // Since Either implements SelectMany we are able to use
            // LINQ like syntacsys. Think of each 'from' clause as a step of a chain
            // if one step fails the further ones willn't be ever executed
            // and you don't need the code from Mark's example like:
            // var validationMsg = _validator.Validate(dto)
            // if (validationMsg) return BadRequest(validationMsg)
            // that is how Either monad works in FP languages (f#, Haskell). 
            // And this is valid c# code you can try it out if you want to.
            var createReservation =
               (from validReservation in _validator.Validate(dto) // pure 
                from _0 in Log(logger => logger.Debug($"The reservation is valid")) // impure
                from existingReservations in _repo.ReadReservations(dto.Date, false) // impure. You can simulate failure if you change false to true and further funcs will not execute
                from _1 in Log(logger => logger.Debug($"Reserved seats count: {existingReservations.Sum(x => x.Quantity)}; Capacity: {CAPACITY}")) // impure
                from acceptedReservation in _service.TryAccept(CAPACITY, dto, existingReservations) // pure domain logic
                from reservationId in _repo.Create(acceptedReservation) // impure
                from _2 in Log(logger => logger.Debug($"The reservation [{acceptedReservation}] has been created.")) // impure
                select reservationId)();

            // if successful
            if (createReservation.IsRight)
                return createReservation.Right;
            else
            {
                // handle failure path
                var errorCode = createReservation.Left;
                switch (errorCode)
                {
                    // looking at this code you can see everything that can get wrong with your 
                    // reservation and pretty convenient if you want to apply localisation for example
                    case CreateReservationStatus.InvalidInput:
                        return "The reservation isn't provided.";
                    case CreateReservationStatus.EmptyEmail:
                        return "The email can't be empty.";
                    case CreateReservationStatus.EmptyName:
                        return "The name can't be empty.";
                    case CreateReservationStatus.NegativeQuantity:
                        return "The seats quantity have to be higher than 0.";
                    case CreateReservationStatus.GetExistingReservationsDbError:
                        return "Can't access the DB.";
                    case CreateReservationStatus.NotEnoughEmptySeats:
                        return "Unfortunately there are not enough seats for you.";
                    case CreateReservationStatus.CreateReservationDbError:
                        return "Can't access the DB.";
                    default:
                        return "Unknown error";
                }
            }
        }

        private Either<CreateReservationStatus, Unit> Log(Action<Logger> act)
        {
            act(_logger);
            return () => Unit.Return(() => { });
        }
    }

    public class Logger
    {
        public void Error(string msg) => WriteLine(msg);

        public void Debug(string msg) => WriteLine(msg);
    }

    public class ReservationValidator
    {
        public Either<CreateReservationStatus, ReservationRequestDto> Validate(ReservationRequestDto dto)
        {
            switch (dto)
            {
                case null:
                    return () => CreateReservationStatus.InvalidInput;
                case ReservationRequestDto _ when string.IsNullOrEmpty(dto.Email):
                    return () => CreateReservationStatus.EmptyEmail;
                case ReservationRequestDto _ when string.IsNullOrEmpty(dto.Name):
                    return () => CreateReservationStatus.EmptyName;
                case ReservationRequestDto _ when dto.Quantity <= 0:
                    return () => CreateReservationStatus.NegativeQuantity;
                default:
                    return () => dto;
            }
        }
    }

    public class ReservationsRepo
    {
        public Either<CreateReservationStatus, IEnumerable<ReservationRequestDto>> ReadReservations(DateTime date, bool isThrow)
        {
            try
            {
                if (isThrow) throw new InvalidOperationException();
                else return () => new List<ReservationRequestDto>() { new ReservationRequestDto { Date = DateTime.Now.Date, Email = "some@email.com", Name = "Reservation1", Quantity = 22 } };
            }
            catch { return () => CreateReservationStatus.GetExistingReservationsDbError; }
        }

        public Either<CreateReservationStatus, int> Create(ReservationRequestDto r) => () => 2;
    }

    public class ReservationsService
    {
        // Pure domain logic. You don't need to use mocks since there are no 
        // any repositories and another impure stuff.
        public Either<CreateReservationStatus, ReservationRequestDto> TryAccept(int capacity,
                                                               ReservationRequestDto reservationToAccept,
                                                               IEnumerable<ReservationRequestDto> existingReservations)
        {
            var reservedSeats = existingReservations.Sum(r => r.Quantity);
            if (reservedSeats + reservationToAccept.Quantity <= capacity)
                return () => reservationToAccept;
            else
                return () => CreateReservationStatus.NotEnoughEmptySeats;
        }
    }

    public class ReservationRequestDto
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Quantity { get; set; }

        public override string ToString() => $"{Name} {Email} {Quantity}";
    }

    public enum CreateReservationStatus
    {
        InvalidInput,
        EmptyEmail,
        EmptyName,
        NegativeQuantity,
        GetExistingReservationsDbError,
        NotEnoughEmptySeats,
        CreateReservationDbError
    }
}
