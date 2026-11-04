create table if not exists public.private_kit_handoffs (
  user_id uuid not null references auth.users(id) on delete cascade,
  stable_job_id text not null,
  resume_md text,
  cover_letter_md text,
  created_at timestamptz not null default now(),
  constraint private_kit_handoffs_pkey primary key (user_id, stable_job_id),
  constraint private_kit_handoffs_has_document check (
    nullif(btrim(resume_md), '') is not null
    or nullif(btrim(cover_letter_md), '') is not null
  )
);

alter table public.private_kit_handoffs enable row level security;

create policy "Users can read their own pending kit handoffs"
  on public.private_kit_handoffs for select to authenticated
  using ((select auth.uid()) is not null and (select auth.uid()) = user_id);

create policy "Users can delete their own pending kit handoffs"
  on public.private_kit_handoffs for delete to authenticated
  using ((select auth.uid()) is not null and (select auth.uid()) = user_id);

revoke all on public.private_kit_handoffs from public, anon, authenticated;
grant select, delete on public.private_kit_handoffs to authenticated;
grant all on public.private_kit_handoffs to service_role;
