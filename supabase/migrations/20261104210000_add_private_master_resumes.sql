create table if not exists public.master_resumes (
  user_id uuid primary key references auth.users(id) on delete cascade,
  content_md text not null,
  source_filename text,
  content_sha256 text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint master_resumes_content_not_empty check (length(btrim(content_md)) > 0)
);

alter table public.master_resumes enable row level security;

create policy "Users can read their own master resume"
  on public.master_resumes for select to authenticated
  using ((select auth.uid()) is not null and (select auth.uid()) = user_id);

create policy "Users can insert their own master resume"
  on public.master_resumes for insert to authenticated
  with check ((select auth.uid()) is not null and (select auth.uid()) = user_id);

create policy "Users can update their own master resume"
  on public.master_resumes for update to authenticated
  using ((select auth.uid()) is not null and (select auth.uid()) = user_id)
  with check ((select auth.uid()) is not null and (select auth.uid()) = user_id);

create policy "Users can delete their own master resume"
  on public.master_resumes for delete to authenticated
  using ((select auth.uid()) is not null and (select auth.uid()) = user_id);

revoke all on public.master_resumes from anon;
grant select, insert, update, delete on public.master_resumes to authenticated;
