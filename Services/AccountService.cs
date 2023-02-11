using BotterDog.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CSharpFunctionalExtensions;
using System.IO;
using Newtonsoft.Json;
using FiresStuff.Services;

namespace BotterDog.Services
{
    public class AccountService
    {
        public List<DogAccount> Accounts { get; private set; }

        private readonly BotLogService _botLog;

        public AccountService(BotLogService botlog)
        {
            _botLog = botlog;
        }

        public Result<DogAccount> CreateAccount(ulong id, decimal startingBalance = 100)
        {
            var accnt = Accounts.FirstOrDefault(x => x.Id == id);

            if (accnt != null)
            {
                return Result.Failure<DogAccount>("Account already exists");
            }
            else
            {
                _botLog.BotLogAsync(BotLogSeverity.Good, "New account", $"New account created: <@{id}>");
                var newAccnt = new DogAccount(id, startingBalance);
                Accounts.Add(newAccnt);
                Save(); //TODO: handle result
                return Result.Success(newAccnt);
            }
        }

        public Result<DogAccount> FindOrCreate(ulong id)
        {
            var accnt = Accounts.FirstOrDefault(x=> x.Id == id);

            if(accnt != null)
            {
                return Result.Success(accnt);
            }
            else
            {
                var res = CreateAccount(id);
                return res.IsSuccess ? Result.Success(res.Value) : Result.Failure<DogAccount>("This error shouldn't happen.");
            }
        }

        public Result ModifyBalance(ulong id, decimal modification)
        {
            var accnt = Accounts.FirstOrDefault(x => x.Id == id);

            if (accnt != null)
            {
                accnt.ModifyBalance(decimal.Round(modification, 2));
                return Result.Success();
            }
            else
            {
                return Result.Failure("Account does not exist.");
            }
        }

        public Result Load()
        {
            try
            {
                Accounts = JsonConvert.DeserializeObject<List<DogAccount>>(File.ReadAllText("accounts.json"));
                _botLog.BotLogAsync(BotLogSeverity.Good, "Accounts loaded", "Accounts loaded successfully.");
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Load failure", "Failure while loading occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }

        public Result Save(bool silent = true)
        {
            try
            {
                File.WriteAllText("accounts.json", JsonConvert.SerializeObject(Accounts));
                if (!silent)
                {
                    _botLog.BotLogAsync(BotLogSeverity.Good, "Accounts saved", "Accounts saved succesfuly.");
                }
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Save failure", "Failure while saving occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }
    }
}
