using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;

namespace BookMyDoctor.Features.Users;

public interface IUserService
{
    IReadOnlyList<User> GetAll();

    // These throw ArgumentException for invalid input, KeyNotFoundException for an unknown id and
    // ConflictException when the email is taken or (for Delete) the patient has appointments.
    User Create(CreateUserRequest request);
    User Update(Guid id, UpdateUserRequest request);
    void Delete(Guid id);

    // Adds the demo patients (all with the given password) whose email is not taken yet.
    LoadDefaultsResult LoadDefaults(string password);
}
