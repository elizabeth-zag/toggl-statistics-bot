using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ToggleStatisticsBot;

var telegramToken = Environment.GetEnvironmentVariable("BotToken");
if (string.IsNullOrWhiteSpace(telegramToken))
{
    Console.WriteLine("BotToken is not set");
    return;
}
using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(telegramToken!, cancellationToken: cts.Token);
var me = await bot.GetMe();
bot.OnMessage += OnMessage;

Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
await Task.Delay(Timeout.Infinite, cts.Token);
async Task OnMessage(Message msg, UpdateType type)
{
    var validate = await Validate(msg);
    if (!validate.isValid)
    {
        return;
    }

    try
    {
        var togglToken = Environment.GetEnvironmentVariable("ToggleToken");
        var togglWorkspace = Environment.GetEnvironmentVariable("ToggleWorkspace");
        var url = $"https://api.track.toggl.com/reports/api/v3/workspace/{togglWorkspace}/search/time_entries";
        using var client = new HttpClient();
        var byteArray = Encoding.UTF8.GetBytes($"{togglToken}:api_token");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        var isNight = DateTime.Now.Hour < 7;

        var request = validate switch
        {
            { dateTimeParams: not null } => GetDateTimeRequest(validate.dateTimeParams),
            { timeParams: not null } => GetTimeRequest(validate.timeParams, isNight),
            { taskType: not null } => GetTaskTypeRequest(validate.taskType, isNight),
            _ => throw new Exception("Unknown response type")
        };
        var taskType = request.Description;

        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        using var responseStream = await client.PostAsync(url, content);
        var responseString = await responseStream.Content.ReadAsStringAsync();
        var response = JsonSerializer.Deserialize<ToggleResponseDto[]>(responseString);
        var offset = response?.FirstOrDefault()?.TimeEntries.FirstOrDefault()?.Start.Offset;
        if (offset is null)
        {
            await bot.SendMessage(msg.Chat, "Sorry, there is no response..... ");
            return;
        }
        var today = new DateTimeOffset(DateTime.Today, offset.Value);

        var startingFilter = validate switch
        {
            { dateTimeParams: not null } => validate.dateTimeParams.Start,
            { timeParams: not null } => isNight && validate.timeParams.Start.Hours > 7
                ? today.AddDays(-1).AddTicks(validate.timeParams.Start.Ticks)
                :today.AddTicks(validate.timeParams.Start.Ticks),
            { taskType: not null } => isNight 
                ? today.AddDays(-1).AddHours(7) 
                : today.AddHours(7),
            _ => throw new Exception("Unknown response type")
        };
        var filteredResponse = response?
            .Where(r => r.Description == taskType)
            .SelectMany(x => x.TimeEntries)
            .Where(r => r.Start.ToUniversalTime() > startingFilter.ToUniversalTime())
            .Select(r => r.Stop - r.Start)
            .ToArray();

        if (filteredResponse is null || filteredResponse.Length == 0)
        {
            await bot.SendMessage(msg.Chat, "Sorry, there is no response..... ");
            return;
        }

        var total = TimeSpan.FromTicks(filteredResponse.Sum(ts => ts.Ticks));
        await bot.SendMessage(msg.Chat, $"Total time is: {total:g}");
    }
    catch
    {
        await bot.SendMessage(msg.Chat, "There was some error..... ");
    }
}

async Task<(bool isValid, DateTimeParams? dateTimeParams, TimeParams? timeParams, string? taskType)>
    Validate(Message msg)
{
    if (msg.Text is null)
    {
        await SendInfoMessage(msg);
        return (false, null, null, null);
    }

    var isDateTime = TryDeserialize(msg.Text, out DateTimeParams? dateTimeParameters);
    var isTime = TryDeserialize(msg.Text, out TimeParams? timeParameters);
    var isTaskType = GetTaskType(msg.Text, out var taskType);

    if (!isDateTime && !isTime && !isTaskType)
    {
        await SendInfoMessage(msg);
        return (false, null, null, null);
    }

    var dateTimeInvalid = dateTimeParameters is null
                          || dateTimeParameters.Start == default
                          || dateTimeParameters.End == default
                          || string.IsNullOrEmpty(dateTimeParameters.Task);
    var timeInvalid = timeParameters is null
                      || timeParameters.Start == TimeSpan.Zero
                      || timeParameters.End == TimeSpan.Zero
                      || string.IsNullOrEmpty(timeParameters.Task);

    if (dateTimeInvalid && timeInvalid && string.IsNullOrWhiteSpace(taskType))
    {
        await SendInfoMessage(msg);
        return (false, null, null, null);
    }

    return (true, dateTimeParameters, timeParameters, taskType);
}

ToggleRequestDto GetDateTimeRequest(DateTimeParams parameters)
{
    return new ToggleRequestDto
    {
        StartDate = parameters.Start.Date.ToString("yyyy-MM-dd"),
        EndDate = parameters.End.Date.ToString("yyyy-MM-dd"),
        Description = parameters.Task
    };
}

ToggleRequestDto GetTimeRequest(TimeParams parameters, bool isNight)
{
    return GetTaskTypeRequest(parameters.Task, isNight);
}

ToggleRequestDto GetTaskTypeRequest(string taskType, bool isNight)
{
    return new ToggleRequestDto
    {
        StartDate = isNight 
            ? DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd") 
            : DateTime.Today.ToString("yyyy-MM-dd"),
        EndDate = DateTime.Today.ToString("yyyy-MM-dd"),
        Description = taskType
    };
}

async Task SendInfoMessage(Message msg)
{
    await bot.SendMessage(msg.Chat, "Text format should be:\n" +
                                    "{\n    \"Start\": \"yyyy-mm-dd hh:mm\",\n    \"End\": \"yyyy-mm-dd hh:mm\",\n    \"Task\": \"TaskType\"\n}" +
                                    "\nOr if you want statistics for today you can just use:\n" +
                                    "{\n    \"Start\": \"hh:mm\",\n    \"End\": \"hh:mm\",\n    \"Task\": \"TaskType\"\n}\n" +
                                    "Or just type \"Task {TaskType}\" and get total for today from 7am");
}

bool TryDeserialize<T>(string json, out T? result) where T : class
{
    try
    {
        result = JsonSerializer.Deserialize<T>(json);
        return result is not null;
    }
    catch
    {
        result = null;
        return false;
    }
}

bool GetTaskType(string text, out string? taskType)
{
    taskType = null;
    var isTask = text.StartsWith("Task ");
    if (!isTask) return false;
    taskType = text.Substring("Task ".Length);
    return true;
}