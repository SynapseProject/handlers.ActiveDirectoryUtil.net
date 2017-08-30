using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Synapse.Core;
using Synapse.ActiveDirectory.Core;
using Synapse.Handlers.ActiveDirectory;

public class GroupMembershipHandler : HandlerRuntimeBase
{
    private GroupMembershipHandlerConfig _config;
    private bool _encounteredFailure = false;
    private int _sequenceNumber = 0;
    private string _context = "Execute";
    private string _mainProgressMsg = "";
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = 0
    };
    private readonly GroupMembershipResponse _response = new GroupMembershipResponse
    {
        Results = new List<Result>()
    };

    public override object GetConfigInstance()
    {
        return new GroupMembershipHandlerConfig()
        {
            ValidDomains = new List<string>()
            {
                "xxx",
                "yyy",
                "zzz"
            }
        };
    }

    public override object GetParametersInstance()
    {
        return new GroupMembershipRequest()
        {
            AddSection = new List<AddSection>()
            {
                new AddSection()
                {
                    Domain = "xxxxxx",
                    Groups = new List<string>()
                    {
                        "xxxxxx"
                    },
                    Users = new List<string>()
                    {
                        "xxxxxx"
                    }
                },
                new AddSection()
                {
                    Domain = "yyyyyy",
                    Groups = new List<string>()
                    {
                        "yyyyyy"
                    },
                    Users = new List<string>()
                    {
                        "yyyyyy"
                    }
                }
            },
            DeleteSection = new List<DeleteSection>()
            {
                new DeleteSection()
                {
                    Domain = "zzzzzz",
                    Groups = new List<string>()
                    {
                        "zzzzzz",
                        "zzzzzz",
                        "zzzzzz"
                    },
                    Users = new List<string>()
                    {
                        "zzzzzz"
                    }
                }
            }
        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        _config = DeserializeOrNew<GroupMembershipHandlerConfig>( values );

        if ( _config?.ValidDomains?.Count > 0 )
        {
            _config.ValidDomains = _config.ValidDomains.ConvertAll( d => d.ToLower() );
        }
        else
        {
            _config = new GroupMembershipHandlerConfig { ValidDomains = new List<string>() };
        }
        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;
        try
        {
            message = "Deserializing incoming request...";
            UpdateProgress( message, StatusType.Initializing );
            string inputParameters = RemoveParameterSingleQuote( startInfo.Parameters );
            GroupMembershipRequest parms = DeserializeOrNew<GroupMembershipRequest>( inputParameters );

            message = "Processing individual child request...";
            UpdateProgress( message, StatusType.Running );
            ProcessAddRequests( parms, startInfo.IsDryRun );
            ProcessDeleteRequests( parms, startInfo.IsDryRun );

            message = "Request has been processed" + (_encounteredFailure ? " with error found" : "") + ".";
            UpdateProgress( message, _encounteredFailure ? StatusType.CompletedWithErrors : StatusType.Success );

        }
        catch ( Exception ex )
        {
            message = $"Execution has been aborted due to: {ex.Message}";
            UpdateProgress( message, StatusType.Failed );
            _encounteredFailure = true;
        }

        _response.Status = message;
        message = "Serializing response...";
        UpdateProgress( message );
        _result.ExitData = JsonConvert.SerializeObject( _response );

        message = startInfo.IsDryRun ? "Dry run execution is completed." : "Execution is completed.";
        UpdateProgress( message, StatusType.Any, true );
        return _result;
    }

    private void UpdateProgress(string message, StatusType status = StatusType.Any, bool isLastStep = false)
    {
        _mainProgressMsg = _mainProgressMsg + Environment.NewLine + message;
        if ( status != StatusType.Any )
        {
            _result.Status = status;
        }
        if ( isLastStep )
        {
            _sequenceNumber = int.MaxValue;
        }
        else
        {
            _sequenceNumber++;
        }
        OnProgress( _context, _mainProgressMsg, _result.Status, _sequenceNumber );
    }

    private void ProcessAddRequests(GroupMembershipRequest parms, bool isDryRun)
    {
        string message;
        int addSectionCount = 0;
        int addGroupCount = 0;
        int addUserCount = 0;
        if ( parms?.AddSection != null )
        {
            foreach ( AddSection addSection in parms.AddSection )
            {
                addSectionCount++;
                foreach ( string group in addSection.Groups )
                {
                    addGroupCount++;
                    foreach ( string user in addSection.Users )
                    {
                        addUserCount++;
                        message = $"Executing add request [{addSectionCount}/{addGroupCount}/{addUserCount}]"
                            + (isDryRun ? " in dry run mode..." : "...");
                        UpdateProgress( message );
                        try
                        {
                            DirectoryServices.AddUserToGroup( user, group, isDryRun, addSection.Domain );
                            Result r = new Result()
                            {
                                Domain = addSection.Domain,
                                User = user,
                                Group = group,
                                Action = "add",
                                ExitCode = 0,
                                Note = isDryRun ? "Dry run has been completed." : "User has been successfully added to the group."
                            };
                            _response.Results.Add( r );
                            message = $"Processed add request [{addSectionCount}/{addGroupCount}/{addUserCount}].";
                            UpdateProgress( message );
                        }
                        catch ( Exception ex )
                        {
                            Result r = new Result()
                            {
                                Domain = addSection.Domain,
                                User = user,
                                Group = group,
                                Action = "add",
                                ExitCode = -1,
                                Note = ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                            message = $"Encountered error while processing add request [{addSectionCount}/{addGroupCount}/{addUserCount}].";
                            UpdateProgress( message );
                        }
                    }
                    addUserCount = 0;
                }
                addGroupCount = 0;
            }
        }
        else
        {
            message = "No add section is found from the incoming request.";
            UpdateProgress( message );
        }
    }

    private void ProcessDeleteRequests(GroupMembershipRequest parms, bool isDryRun)
    {
        string message;
        int deleteSectionCount = 0;
        int deleteGroupCount = 0;
        int deleteUserCount = 0;
        if ( parms?.DeleteSection != null )
        {
            foreach ( DeleteSection deleteSection in parms.DeleteSection )
            {
                deleteSectionCount++;
                foreach ( string group in deleteSection.Groups )
                {
                    deleteGroupCount++;
                    foreach ( string user in deleteSection.Users )
                    {
                        deleteUserCount++;
                        message = $"Executing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}]"
                                           + (isDryRun ? " in dry run mode..." : "...");
                        UpdateProgress( message );
                        try
                        {
                            DirectoryServices.RemoveUserFromGroup( user, group, isDryRun, deleteSection.Domain );
                            Result r = new Result()
                            {
                                Domain = deleteSection.Domain,
                                User = user,
                                Group = group,
                                Action = "delete",
                                ExitCode = 0,
                                Note = isDryRun ? "Dry run has been completed." : "User has been successfully removed from the group."
                            };
                            _response.Results.Add( r );
                            message = $"Processed delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}].";
                            UpdateProgress( message );
                        }
                        catch ( Exception ex )
                        {
                            Result r = new Result()
                            {
                                Domain = deleteSection.Domain,
                                User = user,
                                Group = group,
                                Action = "delete",
                                ExitCode = -1,
                                Note = (isDryRun ? "Dry run has been completed. " : "") + ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                            message = $"Encountered error while processing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}].";
                            UpdateProgress( message );
                        }
                    }
                    deleteUserCount = 0;
                }
                deleteGroupCount = 0;
            }
        }
        else
        {
            message = "No delete section is found from the incoming request.";
            UpdateProgress( message );
        }
    }


    private bool IsValidDomain(string domain)
    {
        bool isValid = false;


        if ( _config == null || _config.ValidDomains.Count == 0 )
        {
            // Domain passed in considered valid if there is no pre-defined valid domains in config.
            isValid = true;
        }
        else if ( String.IsNullOrWhiteSpace( domain ) )
        {
            // Empty domain is a valid equivalent to default domain.
            isValid = true;
        }
        else if ( _config?.ValidDomains != null )
        {
            // Check if domain passed in is among the pre-defined values in the config.
            isValid = _config.ValidDomains.Contains( domain.ToLower() );
        }

        return isValid;
    }

    private static string RemoveParameterSingleQuote(string input)
    {
        string output = "";
        if ( !string.IsNullOrWhiteSpace( input ) )
        {
            Regex pattern = new Regex( ":\\s*'" );
            output = pattern.Replace( input, ": " );
            pattern = new Regex( "'\\s*(\r\n|\r|\n|$)" );
            output = pattern.Replace( output, Environment.NewLine );
        }
        return output;
    }
}

