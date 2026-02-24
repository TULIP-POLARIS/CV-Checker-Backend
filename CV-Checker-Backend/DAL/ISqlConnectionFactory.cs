using Microsoft.Data.SqlClient;

namespace DAL;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}


