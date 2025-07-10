using System.Globalization;

class VisitHandler{

    private List<User> _users = [];
    private uint _online;

    public void OnVisit(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("X-Forwarded-For")) 
        {
            _users.Add(new(context.Connection.RemoteIpAddress!.ToString(), true, false)); _online++;
            return; 
        }
        _users.Add(new(context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()!, true, false)); _online++;
    }

    public uint GetOnline(){
        return _online;
    }
}
class User{
    private readonly string _ip;
    private bool _isOnline;
    private bool _isHourPassed;

    public User(string ip, bool isOnline, bool isHourPassed){
        _ip = ip;
        _isOnline = isOnline;
        _isHourPassed = isHourPassed;
    }
};