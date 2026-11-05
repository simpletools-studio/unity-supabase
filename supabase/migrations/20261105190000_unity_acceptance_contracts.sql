-- Additional isolated acceptance fixtures; no production migration is required by the SDK.
alter table public.unity_acceptance_scores
    add column if not exists tags text[] not null default '{}';

create or replace function public.unity_acceptance_echo(value text)
returns text language sql immutable as $$ select value $$;
revoke all on function public.unity_acceptance_echo(text) from public;
grant execute on function public.unity_acceptance_echo(text) to authenticated;
notify pgrst, 'reload schema';
