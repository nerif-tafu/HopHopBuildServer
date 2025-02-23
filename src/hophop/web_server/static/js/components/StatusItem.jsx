const StatusItem = ({ label, value, colorClass }) => (
    <div className="bg-surface-light p-2 rounded flex flex-col sm:flex-row gap-1 sm:justify-between sm:items-center">
        <span className="text-neutral-600 text-sm">{label}:</span>
        <span className={`${colorClass || 'text-neutral-900'} break-all`}>{value}</span>
    </div>
); 