# snapper-replicator

## Intro

### What is BTRFS?

Btrfs (or B-tree filesystem or Butter FS or Better FS) is a filesystem that many linux distros (SUSE, Fedora, etc.) use as the default fs by
now. It supports subvolumes with atomic, incremental snapshotting functionality at the filesystem level. BTRFS is stable, part of the linux kernel for many years by now.

For more info about BTRFS, please see [BTRFS wiki](https://btrfs.wiki.kernel.org/index.php/Main_Page)

### What is Snapper?

Snapper is a CLI tool that builds upon BTRFS filesystem (although it supports LVM/Ext4 too) providing an integrated
solution for creating/managing/cleaning up snapshots automatically. It can periodically create and cleanup snapshots of your system so you have
a rock solid local backup solution at your disposal.

For more info about Snapper, please see [snapper home](http://snapper.io/)

### What is `snapper-replicator`?

With Btrfs, you can have manual snapshots, with snapper you can have automatic snapshots and with `snapper-replicator`, you can replicate
the snapshots made by snapper between computers. 

Why is this important? Snapper will create the backup snapshots for you locally, but without replicating them somewhere else
you are putting your system into a risk: a hardware failure may make those local snapshots get lost. Also if you don't want snapper to eat up
all storage of your primary system, then you have to configure snapper to do automatic cleanup occasionally. With snapper-replicator, you can have
a full history on a different system which is equipped with more, cheaper storage while keeping some amount of snapshots on your primary system.

Features:
- replicate both full snapshots and increments
- supports both push and pull replication: you can replicate **to** a remote or **from** a remote
- transfers over SSH, using SFTP protocol
- supports pause/resume

## Prerequisites

### Remote system
- a btrfs filesystem
- reachable on SSH, using public key authentication
- at least one BTRFS subvolume, that is maintained by snapper

### Local system

- [GNU Make](https://www.gnu.org/software/make/) - this is optional, but makes life much easier
- a btrfs filesystem
- can reach remote system via SSH
- at least one BTRFS subvolume that is maintained by snapper

## How it works

The replication process has several steps:
- optionally running `snapper cleanup` on the source system, so only the important snapshots are considered
- determining changes between the two systems
- dumping snapshots on the source system (incrementally if possible)
- transferring snapshots between the two systems
- restoring the snapshots on the destination system
- cleaning up

Each step is performed by an individual run of `snapper-replicator` executable, with different CLI arguments
passed to it.

Each step places a signal file in the working directory when it finishes successfully. Combined with
a slightly modified version of the provided example `Makefile`, it is possible to run the replication in a re-entrant
fashion: if the process is interrupted, you can continue from there later.

There is a built in help in the application:
```
USAGE: snapper-replicator.exe [--help] --host <hostname> --mode <push|pull> --key <path> [--user <username>]
                              [--remote-working-directory <path>] [--local-working-directory <path>]
                              [--remote-config <configName>] [--local-config <configName>] [--batch-size <size>] [--verbose]
                              [<subcommand> [<options>]]

SUBCOMMANDS:

    determine-changes <options>
                          determine-changes: Determine difference between local and remote
    dump <options>        Dump snapshots on source
    synchronize <options> Pull snapshots from remote
    restore <options>     Restore snapshots on destination
    clean-remote-workdir <options>
                          Clean up remote working directory
    clean-local-workdir <options>
                          Clean up local working directory
    snapper-cleanup-source <options>
                          Runs `snapper cleanup` on source with the specified cleanup algorithm

    Use 'snapper-replicator.exe <subcommand> --help' for additional information.

OPTIONS:

    --host, -h <hostname> Remote host
    --mode, -m <push|pull>
                          Operation mode: whether to pull or push snapshots
    --key, -k <path>      Path to ssh key
    --user, -u <username> User name on remote host
    --remote-working-directory, -rwd <path>
                          Path to the remote working directory
    --local-working-directory, -lwd <path>
                          Path to the local working directory
    --remote-config, -rc <configName>
                          Name of snapper config on remote
    --local-config, -lc <configName>
                          Name of snapper config locally
    --batch-size, -bs <size>
                          Maximum size of a replication batch
    --verbose, -v         Verbose mode
    --help                display this list of options.

```

There are some top level arguments and some arguments for the individual subcommands.

### Top level arguments

#### Host

Hostname or IP of the remote system. Mandatory.

#### Mode

Operation mode, either `pull` or `push`. If `pull` is specified, snapshots are replicated from the remote system
to the local. If `push` then snapshots are replicated from local to the remote system.

Mandatory.

#### Key

Path to the key file that can be used for connecting to the remote system over SSH.

Mandatory.

#### User

Username to use for the remote SSH connection. Optional parameter, executing user's name will be used if omitted.

#### Remote config

Name of the snapper config on the remote system that is being replicated.

Optional, `root` config is the default.

#### Local config

Name of the snapper config on the local system that is being replicated.

Optional, `root` config is the default.

#### Remote working directory

Path to a directory that can be used for storing the snapshots temporarily on the remote system. 

Optional, `/tmp/snapper-replicator` is the default.

#### Local working directory

Path to a directory that can be used for storing the snapshots temporarily on the local system.

Optional, `/tmp/snapper-replicator` is the default.

#### Batch size

Number of snapshots to be replicated in one turn. Optional, there is no limit if omitted.

#### Verbose

If specified, `snapper-replicator` will print the commands it is executing on the systems. Optional.

### Sub command arguments

#### Algorithm

This is the only mandatory parameter of `snapper-cleanup-source` subcommand. Specifies which `snapper` cleanup
algorithm should run on the source system.

